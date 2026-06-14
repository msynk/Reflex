namespace Reflex;

/// <summary>Configuration for the Reflex manager, populated via <c>AddReflex</c>.</summary>
public sealed class ReflexOptions
{
    internal List<Type> MiddlewareTypes { get; } = [];
    internal List<IReflexMiddleware> MiddlewareInstances { get; } = [];

    /// <summary>Display name shown in Redux DevTools. Defaults to <c>"Reflex"</c>.</summary>
    public string DevToolsName { get; set; } = "Reflex";

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
}
