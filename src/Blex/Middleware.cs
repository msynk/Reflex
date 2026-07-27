using System.Text.Json.Nodes;

namespace Reflex;

/// <summary>
/// A named action argument captured at dispatch time. Generated action/effect wrappers attach
/// their parameters so middleware, subscribers and DevTools can inspect the payload.
/// </summary>
/// <param name="Name">The parameter name (or property name for a standalone <c>Set X</c>).</param>
/// <param name="Value">The argument value (may be <c>null</c>).</param>
public readonly record struct ActionArg(string Name, object? Value);

/// <summary>
/// Context passed to middleware after an action has mutated a store.
/// </summary>
public sealed class ReflexActionContext
{
    private readonly Func<JsonObject>? _capture;
    private JsonObject? _globalState;

    internal ReflexActionContext(IStore store, string actionName, Func<JsonObject> captureGlobalState, int sequence, IReadOnlyList<ActionArg>? args)
    {
        Store = store;
        ActionName = actionName;
        _capture = captureGlobalState;
        Sequence = sequence;
        Args = args ?? [];
        Timestamp = DateTimeOffset.UtcNow;
    }

    /// <summary>The store that produced the action.</summary>
    public IStore Store { get; }

    /// <summary>The action name (e.g. <c>"Increment"</c> or <c>"Set Count"</c>).</summary>
    public string ActionName { get; }

    /// <summary>
    /// Snapshot of the whole application state immediately after the action. Captured lazily on
    /// first access (serializing every store is expensive, and many observers -- persistence, for
    /// example -- never need the full tree). Access it synchronously inside your handler; reading
    /// it after later actions have run would capture their state instead. Treat the returned tree
    /// as read-only: the same instance is shared by every observer of this action.
    /// </summary>
    public JsonObject GlobalState => _globalState ??= _capture!();

    /// <summary>Monotonically increasing action sequence number.</summary>
    public int Sequence { get; }

    /// <summary>
    /// The action's arguments (parameter name/value pairs), captured by the generated wrappers.
    /// Empty for parameterless actions. A standalone <c>Set X</c> carries the assigned value.
    /// </summary>
    public IReadOnlyList<ActionArg> Args { get; }

    /// <summary>When the action completed (UTC).</summary>
    public DateTimeOffset Timestamp { get; }

    private string? _qualifiedName;

    /// <summary>Fully-qualified action label including the originating store.</summary>
    public string QualifiedName => _qualifiedName ??= $"{Store.Name}/{ActionName}";
}

/// <summary>
/// Context passed to middleware <em>before</em> an action mutates a store. Call <see cref="Cancel"/>
/// to veto the action: the mutation will not run and nothing is recorded.
/// </summary>
public sealed class ReflexPreActionContext
{
    internal ReflexPreActionContext(IStore store, string actionName, IReadOnlyList<ActionArg>? args)
    {
        Store = store;
        ActionName = actionName;
        Args = args ?? [];
    }

    /// <summary>The store about to produce the action.</summary>
    public IStore Store { get; }

    /// <summary>The action name (e.g. <c>"Increment"</c> or <c>"Set Count"</c>).</summary>
    public string ActionName { get; }

    /// <summary>
    /// The action's arguments (parameter name/value pairs). Useful for validation filters that
    /// veto based on the payload. Empty for parameterless actions.
    /// </summary>
    public IReadOnlyList<ActionArg> Args { get; }

    private string? _qualifiedName;

    /// <summary>Fully-qualified action label including the originating store.</summary>
    public string QualifiedName => _qualifiedName ??= $"{Store.Name}/{ActionName}";

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
