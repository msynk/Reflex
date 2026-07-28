using System.Text.Json.Nodes;

namespace Blex.Testing;

/// <summary>
/// Records every action dispatched through a <see cref="ManagerBlex"/> for assertions in tests.
/// Dispose (or use a <c>using</c>) to detach. Obtain one via <see cref="TestExtensionsBlex.RecordActions"/>.
/// </summary>
public sealed class ActionLogBlex : IDisposable
{
    private readonly IDisposable _subscription;
    private readonly List<RecordedActionBlex> _actions = [];

    internal ActionLogBlex(ManagerBlex manager)
    {
        _subscription = manager.Subscribe(ctx => _actions.Add(new RecordedActionBlex(
            ctx.Store, ctx.ActionName, ctx.QualifiedName, ctx.Sequence, ctx.GlobalState, ctx.Args)));
    }

    /// <summary>All recorded actions, in dispatch order.</summary>
    public IReadOnlyList<RecordedActionBlex> Actions => _actions;

    /// <summary>The most recently recorded action, or <c>null</c> if none.</summary>
    public RecordedActionBlex? Last => _actions.Count > 0 ? _actions[^1] : null;

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
