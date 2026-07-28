using System.Text.Json.Nodes;

namespace Blex;

/// <summary>A simple middleware that forwards each applied action to a delegate.</summary>
public sealed class DelegateMiddlewareBlex : IMiddlewareBlex
{
    private readonly Action<ActionContextBlex> _handler;

    /// <summary>Creates a middleware from a delegate.</summary>
    public DelegateMiddlewareBlex(Action<ActionContextBlex> handler) => _handler = handler;

    /// <inheritdoc />
    public void OnAction(ActionContextBlex context) => _handler(context);
}
