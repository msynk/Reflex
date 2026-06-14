using System.Text.Json;
using System.Text.Json.Nodes;

namespace Reflex;

/// <summary>
/// The central state manager. Aggregates every registered <see cref="IStore"/> into a single
/// global state tree, runs the middleware pipeline, feeds the DevTools bridge and applies
/// time-travel snapshots. Resolve it from DI if you need cross-store coordination; most apps
/// only interact with individual stores.
/// </summary>
/// <remarks>
/// This type is designed for single-threaded dispatch, matching Blazor's rendering model. The
/// internal <c>_gate</c> only protects the store registry and global-state capture; the dispatch
/// pipeline itself (sequence counter, middleware, DevTools) assumes actions are not dispatched
/// concurrently. Coordinate access externally if you dispatch from multiple threads.
/// </remarks>
public sealed class ReflexManager
{
    private readonly List<IStore> _stores = [];
    private readonly List<IReflexMiddleware> _middleware;
    private readonly Lock _gate = new();
    private IReflexDevTools? _devTools;
    private JsonObject? _initialState;
    private JsonObject? _committedState;
    private int _sequence;
    private bool _connected;

    /// <summary>Creates a manager with the supplied middleware (order preserved).</summary>
    public ReflexManager(IEnumerable<IReflexMiddleware>? middleware = null)
    {
        _middleware = middleware?.ToList() ?? [];
    }

    /// <summary>All registered stores.</summary>
    public IReadOnlyList<IStore> Stores => _stores;

    /// <summary>
    /// Optional sanitizer applied to the state tree just before it is sent to the DevTools extension
    /// (for display only). Use to redact secrets. Note: because DevTools time-travel echoes the
    /// displayed state back, redacting values can corrupt restores - prefer redacting only fields you
    /// never need to jump back to, or disable DevTools entirely in production.
    /// </summary>
    public Func<JsonObject, JsonObject>? DevToolsStateSanitizer { get; set; }

    /// <summary>Optional sanitizer applied to the action label sent to the DevTools extension.</summary>
    public Func<string, string>? DevToolsActionSanitizer { get; set; }

    /// <summary>Returns the first registered store of type <typeparamref name="TStore"/>, or <c>null</c>.</summary>
    public TStore? GetStore<TStore>() where TStore : class, IStore
    {
        lock (_gate)
        {
            foreach (var store in _stores)
            {
                if (store is TStore typed)
                    return typed;
            }
        }

        return null;
    }

    /// <summary>Raised whenever any store changes (after the per-store event).</summary>
    public event Action<ReflexActionContext>? ActionDispatched;

    /// <summary>
    /// Subscribes to dispatched actions, optionally filtered. Returns a token; dispose it to stop
    /// listening. Handler exceptions are isolated so one reactor can't break dispatch. This is the
    /// building block for cross-store coordination (react to one store's action, mutate another).
    /// </summary>
    public IDisposable Subscribe(Action<ReflexActionContext> handler, Func<ReflexActionContext, bool>? filter = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        void Wrapped(ReflexActionContext ctx)
        {
            if (filter is not null && !filter(ctx))
                return;
            try
            {
                handler(ctx);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Reflex] action subscriber failed: {ex.Message}");
            }
        }

        ActionDispatched += Wrapped;
        return new Subscription(() => ActionDispatched -= Wrapped);
    }

    /// <summary>Subscribes to actions originating from a specific store type.</summary>
    public IDisposable SubscribeTo<TStore>(Action<ReflexActionContext> handler) where TStore : IStore
        => Subscribe(handler, ctx => ctx.Store is TStore);

    /// <summary>
    /// Subscribes to actions by name. Matches either the bare action name (e.g. <c>"Increment"</c>)
    /// or the qualified name (e.g. <c>"counter/Increment"</c>).
    /// </summary>
    public IDisposable SubscribeToAction(string actionName, Action<ReflexActionContext> handler)
    {
        ArgumentNullException.ThrowIfNull(actionName);
        return Subscribe(handler, ctx => ctx.ActionName == actionName || ctx.QualifiedName == actionName);
    }

    /// <summary>
    /// Subscribes with an asynchronous reactor (e.g. to trigger an effect on another store). The
    /// returned task is observed; failures are logged rather than surfaced as unobserved exceptions.
    /// </summary>
    public IDisposable SubscribeAsync(Func<ReflexActionContext, Task> handler, Func<ReflexActionContext, bool>? filter = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Subscribe(ctx => ObserveAsync(handler(ctx)), filter);
    }

    private static void ObserveAsync(Task task)
    {
        if (task.IsCompletedSuccessfully)
            return;

        _ = Awaited(task);

        static async Task Awaited(Task t)
        {
            try
            {
                await t.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Reflex] async reactor failed: {ex.Message}");
            }
        }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose()
        {
            _dispose?.Invoke();
            _dispose = null;
        }
    }

    /// <summary>Registers a store with the manager. Idempotent.</summary>
    public void Register(IStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        lock (_gate)
        {
            if (_stores.Contains(store))
                return;
            _stores.Add(store);
            if (store is StoreBase sb)
                sb.Attach(this);
        }
    }

    /// <summary>Attaches a DevTools sink and sends the current state as the initial snapshot.</summary>
    public void ConnectDevTools(IReflexDevTools devTools)
    {
        ArgumentNullException.ThrowIfNull(devTools);
        _devTools = devTools;
        var state = CaptureGlobalState();
        _initialState ??= state;
        _committedState = state;
        _connected = true;
        devTools.Init(SanitizeState(state));
    }

    /// <summary>Builds the global state tree: <c>{ "&lt;StoreName&gt;": &lt;state&gt;, ... }</c>.</summary>
    public JsonObject CaptureGlobalState()
    {
        var root = new JsonObject();
        lock (_gate)
        {
            foreach (var store in _stores)
                root[store.Name] = store.SerializeState();
        }

        return root;
    }

    /// <summary>
    /// Runs the before-action pipeline. Returns <c>false</c> if any middleware vetoed the action,
    /// in which case the caller must skip the mutation. Cheap no-op when no middleware is registered.
    /// </summary>
    internal bool BeforeAction(IStore source, string actionName)
    {
        if (_middleware.Count == 0)
            return true;

        var context = new ReflexPreActionContext(source, actionName);
        foreach (var mw in _middleware)
        {
            try
            {
                mw.BeforeAction(context);
            }
            catch
            {
                // A misbehaving filter must not break dispatch.
            }

            if (context.IsCancelled)
                return false;
        }

        return !context.IsCancelled;
    }

    internal void RecordAction(IStore source, string actionName)
    {
        // Building and serializing the global state on every action is expensive, so skip the
        // whole pipeline when nothing is listening (no middleware, no DevTools, no subscribers).
        if (_middleware.Count == 0 && !_connected && ActionDispatched is null)
            return;

        var global = CaptureGlobalState();
        _initialState ??= global;
        var context = new ReflexActionContext(source, actionName, global, ++_sequence);

        foreach (var mw in _middleware)
        {
            try
            {
                mw.OnAction(context);
            }
            catch
            {
                // Middleware must not break dispatch. Errors are intentionally isolated.
            }
        }

        ActionDispatched?.Invoke(context);

        if (_connected)
            _devTools?.Send(SanitizeAction(context.QualifiedName), SanitizeState(global));
    }

    private JsonObject SanitizeState(JsonObject state)
    {
        if (DevToolsStateSanitizer is null)
            return state;
        try
        {
            return DevToolsStateSanitizer(state) ?? state;
        }
        catch
        {
            return state;
        }
    }

    private string SanitizeAction(string label)
    {
        if (DevToolsActionSanitizer is null)
            return label;
        try
        {
            return DevToolsActionSanitizer(label) ?? label;
        }
        catch
        {
            return label;
        }
    }

    /// <summary>
    /// Handles a raw message from the Redux DevTools extension (JSON serialized). Drives time-travel:
    /// JUMP_TO_STATE / JUMP_TO_ACTION / ROLLBACK / COMMIT / RESET.
    /// </summary>
    public void HandleDevToolsMessage(string messageJson)
    {
        JsonNode? message;
        try
        {
            message = JsonNode.Parse(messageJson);
        }
        catch
        {
            return;
        }

        if (message is null)
            return;

        var type = message["type"]?.GetValue<string>();
        if (type != "DISPATCH")
            return;

        var payloadType = message["payload"]?["type"]?.GetValue<string>();
        switch (payloadType)
        {
            case "JUMP_TO_STATE":
            case "JUMP_TO_ACTION":
                ApplyStateString(message["state"]?.GetValue<string>());
                break;

            case "ROLLBACK":
                // Revert to the last committed snapshot if we have one; otherwise fall back to
                // the state the extension supplied with the message.
                if (_committedState is not null)
                    RestoreGlobalState(_committedState);
                else
                    ApplyStateString(message["state"]?.GetValue<string>());
                break;

            case "RESET":
                if (_initialState is not null)
                    RestoreGlobalState(_initialState);
                _devTools?.Init(SanitizeState(CaptureGlobalState()));
                break;

            case "COMMIT":
                _committedState = CaptureGlobalState();
                _devTools?.Init(SanitizeState(_committedState));
                break;

            case "IMPORT_STATE":
                var computed = message["payload"]?["nextLiftedState"]?["computedStates"]?.AsArray();
                var last = computed?.LastOrDefault();
                if (last?["state"] is JsonObject importState)
                    RestoreGlobalState(importState);
                break;
        }
    }

    private void ApplyStateString(string? stateJson)
    {
        if (string.IsNullOrEmpty(stateJson))
            return;

        if (JsonNode.Parse(stateJson) is JsonObject obj)
            RestoreGlobalState(obj);
    }

    /// <summary>Applies a global state tree to every store without recording new actions.</summary>
    public void RestoreGlobalState(JsonObject global)
    {
        ArgumentNullException.ThrowIfNull(global);
        lock (_gate)
        {
            foreach (var store in _stores)
            {
                if (global[store.Name] is JsonObject slice && store is StoreBase sb)
                {
                    // Clone so the store owns its node (a JsonNode can't be parented twice).
                    var clone = (JsonObject)slice.DeepClone();
                    sb.ApplyRestoredState(clone);
                }
            }
        }
    }
}
