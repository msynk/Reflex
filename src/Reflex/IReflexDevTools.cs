using System.Text.Json.Nodes;

namespace Reflex;

/// <summary>
/// Abstraction over a DevTools sink. The core library depends only on this interface; the concrete
/// Redux DevTools bridge lives in the <c>Reflex.DevTools</c> package so the core has no JS dependency.
/// </summary>
public interface IReflexDevTools
{
    /// <summary>Sends the initial application state when the DevTools session connects.</summary>
    void Init(JsonObject globalState);

    /// <summary>Sends a dispatched action and the resulting global state.</summary>
    void Send(string actionName, JsonObject globalState);
}
