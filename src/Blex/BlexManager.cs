using System.Text.Json;
using System.Text.Json.Nodes;

namespace Blex;

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
public sealed class BlexManager
{
    private readonly List<IStore> _stores = [];
    private readonly List<IBlexMiddleware> _middleware;
    private readonly Lock _gate = new();
    private IBlexDevTools? _devTools;
    private JsonObject? _initialState;
    private JsonObject? _committedState;
    private int _sequence;
    private bool _connected;

    /// <summary>Creates a manager with the supplied middleware (order preserved).</summary>
    public BlexManager(IEnumerable<IBlexMiddleware>? middleware = null)
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

    /// <summary>
    /// Optional sink for non-fatal errors that Blex isolates from the dispatch pipeline
    /// (throwing subscribers, middleware, persistence writes, restores, sanitizers, ...).
    /// When unset, errors are written to <see cref="Console.Error"/>. Handlers must not throw;
    /// a throwing handler is itself swallowed to keep dispatch alive.
    /// </summary>
    public Action<BlexError>? OnError { get; set; }

    /// <summary>
    /// Whether anything observes dispatched actions (middleware, DevTools or subscribers).
    /// Lets hot paths skip payload capture when nobody is listening.
    /// </summary>
    internal bool HasObservers => _middleware.Count > 0 || _connected || ActionDispatched is not null;

    /// <summary>Routes an isolated error to <see cref="OnError"/>, or <see cref="Console.Error"/> when unset.</summary>
    internal void ReportError(string source, Exception exception, string? detail = null)
    {
        var handler = OnError;
        if (handler is null)
        {
            Console.Error.WriteLine($"[Blex] {source} failed{(detail is null ? "" : $" ({detail})")}: {exception.Message}");
            return;
        }

        try
        {
            handler(new BlexError(source, exception, detail));
        }
        catch
        {
            // The error handler is the last line of defense; nothing left to do.
        }
    }

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
    public event Action<BlexActionContext>? ActionDispatched;

    /// <summary>
    /// Subscribes to dispatched actions, optionally filtered. Returns a token; dispose it to stop
    /// listening. Handler exceptions are isolated so one reactor can't break dispatch. This is the
    /// building block for cross-store coordination (react to one store's action, mutate another).
    /// </summary>
    public IDisposable Subscribe(Action<BlexActionContext> handler, Func<BlexActionContext, bool>? filter = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        void Wrapped(BlexActionContext ctx)
        {
            if (filter is not null && !filter(ctx))
                return;
            try
            {
                handler(ctx);
            }
            catch (Exception ex)
            {
                ReportError("subscriber", ex, ctx.QualifiedName);
            }
        }

        ActionDispatched += Wrapped;
        return new Subscription(() => ActionDispatched -= Wrapped);
    }

    /// <summary>Subscribes to actions originating from a specific store type.</summary>
    public IDisposable SubscribeTo<TStore>(Action<BlexActionContext> handler) where TStore : IStore
        => Subscribe(handler, ctx => ctx.Store is TStore);

    /// <summary>
    /// Subscribes to actions by name. Matches either the bare action name (e.g. <c>"Increment"</c>)
    /// or the qualified name (e.g. <c>"counter/Increment"</c>).
    /// </summary>
    public IDisposable SubscribeToAction(string actionName, Action<BlexActionContext> handler)
    {
        ArgumentNullException.ThrowIfNull(actionName);
        return Subscribe(handler, ctx => ctx.ActionName == actionName || ctx.QualifiedName == actionName);
    }

    /// <summary>
    /// Subscribes with an asynchronous reactor (e.g. to trigger an effect on another store). The
    /// returned task is observed; failures are logged rather than surfaced as unobserved exceptions.
    /// </summary>
    public IDisposable SubscribeAsync(Func<BlexActionContext, Task> handler, Func<BlexActionContext, bool>? filter = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Subscribe(ctx => ObserveAsync(handler(ctx)), filter);
    }

    private void ObserveAsync(Task task)
    {
        if (task.IsCompletedSuccessfully)
            return;

        _ = Awaited(this, task);

        static async Task Awaited(BlexManager manager, Task t)
        {
            try
            {
                await t.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                manager.ReportError("async reactor", ex);
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

    /// <summary>
    /// Removes a store from the manager (e.g. when a lazily-loaded feature is torn down).
    /// The store keeps working standalone; its actions are simply no longer observed.
    /// </summary>
    public void Unregister(IStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        lock (_gate)
        {
            if (_stores.Remove(store) && store is StoreBase sb)
                sb.Detach();
        }
    }

    /// <summary>Attaches a DevTools sink and sends the current state as the initial snapshot.</summary>
    public void ConnectDevTools(IBlexDevTools devTools)
    {
        ArgumentNullException.ThrowIfNull(devTools);
        _devTools = devTools;
        var state = CaptureGlobalState();
        _initialState ??= state;
        _committedState = state;
        _connected = true;
        devTools.Init(SanitizeState(state));
    }

    /// <summary>Detaches the DevTools sink (e.g. when the bridge is disposed).</summary>
    public void DisconnectDevTools()
    {
        _devTools = null;
        _connected = false;
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
    /// <summary>
    /// Actions whose observers are currently being notified. A reactor that dispatches a
    /// follow-up action would otherwise change state before a later observer of the *original*
    /// action lazily captures its snapshot; freezing in-flight snapshots first keeps attribution
    /// correct.
    /// </summary>
    private readonly List<BlexActionContext> _inFlight = [];

    private void FreezeInFlightSnapshots()
    {
        for (var i = 0; i < _inFlight.Count; i++)
            _ = _inFlight[i].GlobalState;
    }

    internal bool BeforeAction(IStore source, string actionName, IReadOnlyList<ActionArg>? args = null)
    {
        // This runs just before a new mutation; pin the snapshots of any actions still being
        // observed so their lazily-captured GlobalState reflects *their* state, not this one's.
        if (_inFlight.Count > 0)
            FreezeInFlightSnapshots();

        if (_middleware.Count == 0)
            return true;

        var context = new BlexPreActionContext(source, actionName, args);
        foreach (var mw in _middleware)
        {
            try
            {
                mw.BeforeAction(context);
            }
            catch (Exception ex)
            {
                // A misbehaving filter must not break dispatch.
                ReportError("middleware", ex, context.QualifiedName);
            }

            if (context.IsCancelled)
                return false;
        }

        return !context.IsCancelled;
    }

    internal void RecordAction(IStore source, string actionName, IReadOnlyList<ActionArg>? args = null)
    {
        // Skip the whole pipeline when nothing is listening (no middleware, no DevTools, no
        // subscribers). The global state itself is captured lazily by the context, so observers
        // that never read it (e.g. persistence) don't pay for full-tree serialization either.
        if (!HasObservers)
            return;

        var context = new BlexActionContext(source, actionName, CaptureGlobalState, ++_sequence, args);

        _inFlight.Add(context);
        try
        {
            foreach (var mw in _middleware)
            {
                try
                {
                    mw.OnAction(context);
                }
                catch (Exception ex)
                {
                    // Middleware must not break dispatch. Errors are isolated and reported.
                    ReportError("middleware", ex, context.QualifiedName);
                }
            }

            // DevTools must see this action before any subscriber reacts: a reactor that
            // dispatches a follow-up action re-enters RecordAction, and sending that first would
            // invert the extension's timeline (breaking time-travel ordering).
            if (_connected && _devTools is not null)
            {
                var global = context.GlobalState;
                _initialState ??= global;
                _devTools.Send(SanitizeAction(context.QualifiedName), SanitizeState(global), BuildDevToolsPayload(context.Args));
            }

            ActionDispatched?.Invoke(context);
        }
        finally
        {
            _inFlight.RemoveAt(_inFlight.Count - 1);
        }
    }

    private JsonObject? BuildDevToolsPayload(IReadOnlyList<ActionArg> args)
    {
        if (args.Count == 0)
            return null;

        var payload = new JsonObject();
        foreach (var arg in args)
        {
            JsonNode? node;
            try
            {
                node = JsonSerializer.SerializeToNode(arg.Value, BlexJson.Options);
            }
            catch
            {
                node = "<unserializable>";
            }

            payload[arg.Name] = node;
        }

        // Run the payload through the state sanitizer as well so key-based redaction
        // (RedactDevToolsKeys) covers action arguments too.
        return SanitizeState(payload);
    }

    private JsonObject SanitizeState(JsonObject state)
    {
        if (DevToolsStateSanitizer is null)
            return state;
        try
        {
            return DevToolsStateSanitizer(state) ?? state;
        }
        catch (Exception ex)
        {
            // Fail closed: a broken redaction sanitizer must not leak the data it was meant to
            // hide, so an empty tree is sent instead of the unsanitized one.
            ReportError("devtools", ex, "state sanitizer failed; state withheld from DevTools");
            return new JsonObject();
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
        catch (Exception ex)
        {
            // Fail closed, mirroring SanitizeState.
            ReportError("devtools", ex, "action sanitizer failed; label withheld from DevTools");
            return "<sanitizer-error>";
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

        try
        {
            if (JsonNode.Parse(stateJson) is JsonObject obj)
                RestoreGlobalState(obj);
        }
        catch (Exception ex)
        {
            // A malformed or schema-drifted DevTools snapshot must not crash the interop callback.
            ReportError("devtools", ex, "time-travel restore");
        }
    }

    /// <summary>
    /// Raised after <see cref="RestoreGlobalState"/> applies a snapshot (time-travel, undo/redo).
    /// Persistence uses this to write the restored state back to storage so a reload does not
    /// resurrect the pre-restore state.
    /// </summary>
    public event Action? StateRestored;

    /// <summary>
    /// Applies a global state tree to every store without recording new actions. A store whose
    /// slice fails to deserialize is reported via <see cref="OnError"/> and skipped; the other
    /// stores still restore.
    /// </summary>
    public void RestoreGlobalState(JsonObject global)
    {
        ArgumentNullException.ThrowIfNull(global);

        // A restore triggered from inside an observer (e.g. a reactor calling history.Undo())
        // mutates state just like a new action would; pin in-flight snapshots first so they
        // keep the state that belonged to their own actions.
        if (_inFlight.Count > 0)
            FreezeInFlightSnapshots();

        // Snapshot the registry, then apply outside the lock: ApplyRestoredState raises
        // StateChanged, and a handler that registers/unregisters a store must not deadlock or
        // invalidate the enumeration.
        IStore[] stores;
        lock (_gate)
        {
            stores = [.. _stores];
        }

        foreach (var store in stores)
        {
            if (global[store.Name] is JsonObject slice && store is StoreBase sb)
            {
                try
                {
                    // Clone so the store owns its node (a JsonNode can't be parented twice).
                    var clone = (JsonObject)slice.DeepClone();
                    sb.ApplyRestoredState(clone);
                }
                catch (Exception ex)
                {
                    ReportError("restore", ex, store.Name);
                }
            }
        }

        StateRestored?.Invoke();
    }
}
