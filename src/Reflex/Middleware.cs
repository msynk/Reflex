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
/// Context passed to middleware <em>before</em> an action mutates a store. Call <see cref="Cancel"/>
/// to veto the action: the mutation will not run and nothing is recorded.
/// </summary>
public sealed class ReflexPreActionContext
{
    internal ReflexPreActionContext(IStore store, string actionName)
    {
        Store = store;
        ActionName = actionName;
    }

    /// <summary>The store about to produce the action.</summary>
    public IStore Store { get; }

    /// <summary>The action name (e.g. <c>"Increment"</c> or <c>"Set Count"</c>).</summary>
    public string ActionName { get; }

    /// <summary>Fully-qualified action label including the originating store.</summary>
    public string QualifiedName => $"{Store.Name}/{ActionName}";

    /// <summary>Whether a middleware has vetoed this action.</summary>
    public bool IsCancelled { get; private set; }

    /// <summary>Vetoes the action so its mutation does not run.</summary>
    public void Cancel() => IsCancelled = true;
}

/// <summary>
/// A pipeline hook invoked for every dispatched action. Use for logging, analytics, persistence, etc.
/// Middleware runs synchronously and must not throw; exceptions are swallowed and reported to other middleware.
/// </summary>
public interface IReflexMiddleware
{
    /// <summary>Invoked after an action has been applied to its store.</summary>
    void OnAction(ReflexActionContext context);

    /// <summary>
    /// Invoked before an action runs. Override to inspect or <see cref="ReflexPreActionContext.Cancel">veto</see>
    /// the action. The default implementation does nothing (the action proceeds).
    /// </summary>
    void BeforeAction(ReflexPreActionContext context)
    {
    }
}

/// <summary>A simple middleware that forwards each applied action to a delegate.</summary>
public sealed class DelegateMiddleware : IReflexMiddleware
{
    private readonly Action<ReflexActionContext> _handler;

    /// <summary>Creates a middleware from a delegate.</summary>
    public DelegateMiddleware(Action<ReflexActionContext> handler) => _handler = handler;

    /// <inheritdoc />
    public void OnAction(ReflexActionContext context) => _handler(context);
}

/// <summary>
/// A middleware that can veto actions before they run. The delegate returns <c>false</c> to cancel.
/// </summary>
public sealed class FilterMiddleware : IReflexMiddleware
{
    private readonly Func<ReflexPreActionContext, bool> _filter;

    /// <summary>Creates a filter; return <c>false</c> from <paramref name="filter"/> to veto the action.</summary>
    public FilterMiddleware(Func<ReflexPreActionContext, bool> filter) => _filter = filter;

    /// <inheritdoc />
    public void OnAction(ReflexActionContext context)
    {
    }

    /// <inheritdoc />
    public void BeforeAction(ReflexPreActionContext context)
    {
        if (!_filter(context))
            context.Cancel();
    }
}
