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
    private readonly List<IStore> _stores = new();
    private readonly List<IReflexMiddleware> _middleware;
    private readonly object _gate = new();
    private IReflexDevTools? _devTools;
    private JsonObject? _initialState;
    private JsonObject? _committedState;
    private int _sequence;
    private bool _connected;

    /// <summary>Creates a manager with the supplied middleware (order preserved).</summary>
    public ReflexManager(IEnumerable<IReflexMiddleware>? middleware = null)
    {
        _middleware = middleware?.ToList() ?? new List<IReflexMiddleware>();
    }

    /// <summary>All registered stores.</summary>
    public IReadOnlyList<IStore> Stores => _stores;

    /// <summary>Raised whenever any store changes (after the per-store event).</summary>
    public event Action<ReflexActionContext>? ActionDispatched;

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
        devTools.Init(state);
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
            _devTools?.Send(context.QualifiedName, global);
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
                _devTools?.Init(CaptureGlobalState());
                break;

            case "COMMIT":
                _committedState = CaptureGlobalState();
                _devTools?.Init(_committedState);
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
