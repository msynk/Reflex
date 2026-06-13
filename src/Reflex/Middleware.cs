using System.Text.Json.Nodes;

namespace Reflex;

/// <summary>
/// Context passed to middleware after an action has mutated a store.
/// </summary>
public sealed class ReflexActionContext
{
    internal ReflexActionContext(IStore store, string actionName, JsonObject globalState, int sequence)
    {
        Store = store;
        ActionName = actionName;
        GlobalState = globalState;
        Sequence = sequence;
        Timestamp = DateTimeOffset.UtcNow;
    }

    /// <summary>The store that produced the action.</summary>
    public IStore Store { get; }

    /// <summary>The action name (e.g. <c>"Increment"</c> or <c>"Set Count"</c>).</summary>
    public string ActionName { get; }

    /// <summary>Snapshot of the whole application state immediately after the action.</summary>
    public JsonObject GlobalState { get; }

    /// <summary>Monotonically increasing action sequence number.</summary>
    public int Sequence { get; }

    /// <summary>When the action completed (UTC).</summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>Fully-qualified action label including the originating store.</summary>
    public string QualifiedName => $"{Store.Name}/{ActionName}";
}

/// <summary>
/// A pipeline hook invoked for every dispatched action. Use for logging, analytics, persistence, etc.
/// Middleware runs synchronously and must not throw; exceptions are swallowed and reported to other middleware.
/// </summary>
public interface IReflexMiddleware
{
    /// <summary>Invoked after an action has been applied to its store.</summary>
    void OnAction(ReflexActionContext context);
}

/// <summary>A simple middleware that forwards each action to a delegate.</summary>
public sealed class DelegateMiddleware : IReflexMiddleware
{
    private readonly Action<ReflexActionContext> _handler;

    /// <summary>Creates a middleware from a delegate.</summary>
    public DelegateMiddleware(Action<ReflexActionContext> handler) => _handler = handler;

    /// <inheritdoc />
    public void OnAction(ReflexActionContext context) => _handler(context);
}
