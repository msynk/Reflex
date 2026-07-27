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
            return new ReflexManager(resolved)
            {
                DevToolsStateSanitizer = opts.DevToolsStateSanitizer,
                DevToolsActionSanitizer = opts.DevToolsActionSanitizer,
                OnError = opts.OnError,
            };
        });

        return services;
    }

    /// <summary>
    /// Registers a store as a scoped service exposed both as its concrete type and as <see cref="IStore"/>.
    /// The store is attached to the <see cref="ReflexManager"/> on first resolution, so actions are
    /// observed even in hosts without a <c>&lt;ReflexProvider&gt;</c> (console apps, workers, tests).
    /// </summary>
    public static IServiceCollection AddReflexStore<TStore>(this IServiceCollection services)
        where TStore : StoreBase
    {
        services.AddScoped(sp =>
        {
            var store = ActivatorUtilities.CreateInstance<TStore>(sp);
            sp.GetRequiredService<ReflexManager>().Register(store);
            return store;
        });
        services.AddScoped<IStore>(sp => sp.GetRequiredService<TStore>());
        return services;
    }

    /// <summary>
    /// Enables automatic persistence for stores marked <c>[Store(Persist = true)]</c>. Requires an
    /// <see cref="IReflexStorage"/> to be registered (for Blazor use <c>AddReflexLocalStoragePersistence</c>
    /// or <c>AddReflexSessionStoragePersistence</c> from <c>Reflex.Blazor</c>).
    /// </summary>
    public static IServiceCollection AddReflexPersistence(
        this IServiceCollection services,
        Action<ReflexPersistenceOptions>? configure = null)
    {
        var options = new ReflexPersistenceOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddScoped(sp => new StatePersistor(
            sp.GetRequiredService<ReflexManager>(),
            sp.GetRequiredService<IReflexStorage>(),
            sp.GetRequiredService<ReflexPersistenceOptions>()));
        return services;
    }

    /// <summary>
    /// Registers in-app undo/redo (<see cref="ReflexHistory"/>). Resolve <see cref="ReflexHistory"/>
    /// to call <c>Undo()</c>/<c>Redo()</c>; <c>&lt;ReflexProvider&gt;</c> starts recording automatically.
    /// </summary>
    public static IServiceCollection AddReflexHistory(this IServiceCollection services, int maxEntries = 100)
    {
        services.AddScoped(sp => new ReflexHistory(sp.GetRequiredService<ReflexManager>(), maxEntries));
        return services;
    }
}
