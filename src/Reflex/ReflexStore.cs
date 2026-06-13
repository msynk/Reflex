using System.Text.Json;
using System.Text.Json.Nodes;

namespace Reflex;

/// <summary>
/// The central state manager. Aggregates every registered <see cref="IStore"/> into a single
/// global state tree, runs the middleware pipeline, feeds the DevTools bridge and applies
/// time-travel snapshots. Resolve it from DI if you need cross-store coordination; most apps
/// only interact with individual stores.
/// </summary>
public sealed class ReflexStore
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
    public ReflexStore(IEnumerable<IReflexMiddleware>? middleware = null)
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
        _initialState ??= CaptureGlobalState();
        _committedState = _initialState;
        _connected = true;
        devTools.Init(CaptureGlobalState());
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
        _initialState ??= CaptureGlobalState();
        var global = CaptureGlobalState();
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
            case "ROLLBACK":
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
                    var clone = (JsonObject)JsonNode.Parse(slice.ToJsonString())!;
                    sb.ApplyRestoredState(clone);
                }
            }
        }
    }
}
