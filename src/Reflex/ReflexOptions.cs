using System.Text.Json.Nodes;

namespace Reflex;

/// <summary>Configuration for the Reflex manager, populated via <c>AddReflex</c>.</summary>
public sealed class ReflexOptions
{
    internal List<Type> MiddlewareTypes { get; } = [];
    internal List<IReflexMiddleware> MiddlewareInstances { get; } = [];

    /// <summary>Display name shown in Redux DevTools. Defaults to <c>"Reflex"</c>.</summary>
    public string DevToolsName { get; set; } = "Reflex";

    /// <summary>
    /// Sanitizer applied to the state tree before it is sent to DevTools (display only). Wired onto
    /// <see cref="ReflexManager.DevToolsStateSanitizer"/>. See the remarks there for the time-travel caveat.
    /// </summary>
    public Func<JsonObject, JsonObject>? DevToolsStateSanitizer { get; set; }

    /// <summary>Sanitizer applied to the action label sent to DevTools.</summary>
    public Func<string, string>? DevToolsActionSanitizer { get; set; }

    /// <summary>
    /// Convenience: redacts the named top-level keys (recursively, anywhere in the tree) from the
    /// state sent to DevTools, replacing their values with <c>"&lt;redacted&gt;"</c>.
    /// </summary>
    public ReflexOptions RedactDevToolsKeys(params string[] keys)
    {
        var set = new HashSet<string>(keys, StringComparer.Ordinal);
        DevToolsStateSanitizer = state => RedactKeys(state, set);
        return this;
    }

    private static JsonObject RedactKeys(JsonObject source, HashSet<string> keys)
    {
        var clone = (JsonObject)source.DeepClone();
        Walk(clone);
        return clone;

        void Walk(JsonObject obj)
        {
            foreach (var key in obj.Select(kvp => kvp.Key).ToList())
            {
                if (keys.Contains(key))
                    obj[key] = "<redacted>";
                else if (obj[key] is JsonObject child)
                    Walk(child);
            }
        }
    }

    /// <summary>Registers a middleware type resolved from DI.</summary>
    public ReflexOptions UseMiddleware<TMiddleware>() where TMiddleware : class, IReflexMiddleware
    {
        MiddlewareTypes.Add(typeof(TMiddleware));
        return this;
    }

    /// <summary>Registers a pre-built middleware instance.</summary>
    /// <remarks>
    /// The instance is stored on the (singleton) options and therefore shared across every DI
    /// scope. Under Blazor Server that means it is shared across all circuits, so avoid holding
    /// per-user state in it. Use <see cref="UseMiddleware{TMiddleware}"/> for scoped, per-circuit
    /// middleware.
    /// </remarks>
    public ReflexOptions UseMiddleware(IReflexMiddleware middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        MiddlewareInstances.Add(middleware);
        return this;
    }

    /// <summary>Registers a delegate-based middleware (handy for quick logging).</summary>
    public ReflexOptions UseMiddleware(Action<ReflexActionContext> handler)
        => UseMiddleware(new DelegateMiddleware(handler));

    /// <summary>
    /// Registers a veto filter that runs before each action. Return <c>false</c> to cancel the
    /// action so its mutation never runs (e.g. guard rails, read-only modes, validation).
    /// </summary>
    public ReflexOptions UseFilter(Func<ReflexPreActionContext, bool> filter)
        => UseMiddleware(new FilterMiddleware(filter));
}
