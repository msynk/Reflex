using System.Text.Json.Nodes;

namespace Blex;

/// <summary>
/// A middleware that can veto actions before they run. The delegate returns <c>false</c> to cancel.
/// </summary>
public sealed class FilterMiddlewareBlex : IMiddlewareBlex
{
    private readonly Func<PreActionContextBlex, bool> _filter;

    /// <summary>Creates a filter; return <c>false</c> from <paramref name="filter"/> to veto the action.</summary>
    public FilterMiddlewareBlex(Func<PreActionContextBlex, bool> filter) => _filter = filter;

    /// <inheritdoc />
    public void OnAction(ActionContextBlex context)
    {
    }

    /// <inheritdoc />
    public void BeforeAction(PreActionContextBlex context)
    {
        if (!_filter(context))
            context.Cancel();
    }
}
