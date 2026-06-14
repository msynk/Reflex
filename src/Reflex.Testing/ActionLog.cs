using System.Text.Json.Nodes;

namespace Reflex.Testing;

/// <summary>A single recorded action and the state snapshot taken immediately after it.</summary>
/// <param name="Store">The store that produced the action.</param>
/// <param name="ActionName">The bare action name (e.g. <c>"Increment"</c>).</param>
/// <param name="QualifiedName">The qualified name (e.g. <c>"counter/Increment"</c>).</param>
/// <param name="Sequence">The monotonic action sequence number.</param>
/// <param name="State">The global state tree right after the action.</param>
public sealed record RecordedAction(
    IStore Store,
    string ActionName,
    string QualifiedName,
    int Sequence,
    JsonObject State);

/// <summary>
/// Records every action dispatched through a <see cref="ReflexManager"/> for assertions in tests.
/// Dispose (or use a <c>using</c>) to detach. Obtain one via <see cref="ReflexTestExtensions.RecordActions"/>.
/// </summary>
public sealed class ActionLog : IDisposable
{
    private readonly IDisposable _subscription;
    private readonly List<RecordedAction> _actions = [];

    internal ActionLog(ReflexManager manager)
    {
        _subscription = manager.Subscribe(ctx => _actions.Add(new RecordedAction(
            ctx.Store, ctx.ActionName, ctx.QualifiedName, ctx.Sequence, ctx.GlobalState)));
    }

    /// <summary>All recorded actions, in dispatch order.</summary>
    public IReadOnlyList<RecordedAction> Actions => _actions;

    /// <summary>The most recently recorded action, or <c>null</c> if none.</summary>
    public RecordedAction? Last => _actions.Count > 0 ? _actions[^1] : null;

    /// <summary>Number of recorded actions.</summary>
    public int Count => _actions.Count;

    /// <summary>The bare action names in order.</summary>
    public IReadOnlyList<string> Names => _actions.Select(a => a.ActionName).ToList();

    /// <summary>The qualified action names in order.</summary>
    public IReadOnlyList<string> QualifiedNames => _actions.Select(a => a.QualifiedName).ToList();

    /// <summary>Whether an action with the given bare or qualified name was recorded.</summary>
    public bool Contains(string actionName)
        => _actions.Any(a => a.ActionName == actionName || a.QualifiedName == actionName);

    /// <summary>How many times an action with the given bare or qualified name was recorded.</summary>
    public int CountOf(string actionName)
        => _actions.Count(a => a.ActionName == actionName || a.QualifiedName == actionName);

    /// <summary>Clears the recorded actions.</summary>
    public void Clear() => _actions.Clear();

    /// <inheritdoc />
    public void Dispose() => _subscription.Dispose();
}
