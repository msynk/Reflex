using Microsoft.Extensions.DependencyInjection;

namespace Reflex;

/// <summary>DI helpers for registering Reflex and its stores.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Reflex manager and middleware. Call <see cref="AddReflexStore{TStore}"/> for each store.
    /// </summary>
    public static IServiceCollection AddReflex(this IServiceCollection services, Action<ReflexOptions>? configure = null)
    {
        var options = new ReflexOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        foreach (var type in options.MiddlewareTypes)
            services.AddScoped(typeof(IReflexMiddleware), type);

        services.AddScoped(sp =>
        {
            var opts = sp.GetRequiredService<ReflexOptions>();
            var resolved = sp.GetServices<IReflexMiddleware>().ToList();
            resolved.AddRange(opts.MiddlewareInstances);
            return new ReflexManager(resolved);
        });

        return services;
    }

    /// <summary>
    /// Registers a store as a scoped service exposed both as its concrete type and as <see cref="IStore"/>.
    /// </summary>
    public static IServiceCollection AddReflexStore<TStore>(this IServiceCollection services)
        where TStore : StoreBase
    {
        services.AddScoped<TStore>();
        services.AddScoped<IStore>(sp => sp.GetRequiredService<TStore>());
        return services;
    }
}
