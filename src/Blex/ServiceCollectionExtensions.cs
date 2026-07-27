using Microsoft.Extensions.DependencyInjection;

namespace Blex;

/// <summary>DI helpers for registering Blex and its stores.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Blex manager and middleware. Call <see cref="AddBlexStore{TStore}"/> for each store.
    /// </summary>
    public static IServiceCollection AddBlex(this IServiceCollection services, Action<BlexOptions>? configure = null)
    {
        var options = new BlexOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        foreach (var type in options.MiddlewareTypes)
            services.AddScoped(typeof(IBlexMiddleware), type);

        services.AddScoped(sp =>
        {
            var opts = sp.GetRequiredService<BlexOptions>();
            var resolved = sp.GetServices<IBlexMiddleware>().ToList();
            resolved.AddRange(opts.MiddlewareInstances);
            return new BlexManager(resolved)
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
    /// The store is attached to the <see cref="BlexManager"/> on first resolution, so actions are
    /// observed even in hosts without a <c>&lt;BlexProvider&gt;</c> (console apps, workers, tests).
    /// </summary>
    public static IServiceCollection AddBlexStore<TStore>(this IServiceCollection services)
        where TStore : StoreBase
    {
        services.AddScoped(sp =>
        {
            var store = ActivatorUtilities.CreateInstance<TStore>(sp);
            sp.GetRequiredService<BlexManager>().Register(store);
            return store;
        });
        services.AddScoped<IStore>(sp => sp.GetRequiredService<TStore>());
        return services;
    }

    /// <summary>
    /// Enables automatic persistence for stores marked <c>[Store(Persist = true)]</c>. Requires an
    /// <see cref="IBlexStorage"/> to be registered (for Blazor use <c>AddBlexLocalStoragePersistence</c>
    /// or <c>AddBlexSessionStoragePersistence</c> from <c>Blex.Blazor</c>).
    /// </summary>
    public static IServiceCollection AddBlexPersistence(
        this IServiceCollection services,
        Action<BlexPersistenceOptions>? configure = null)
    {
        var options = new BlexPersistenceOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddScoped(sp => new StatePersistor(
            sp.GetRequiredService<BlexManager>(),
            sp.GetRequiredService<IBlexStorage>(),
            sp.GetRequiredService<BlexPersistenceOptions>()));
        return services;
    }

    /// <summary>
    /// Registers in-app undo/redo (<see cref="BlexHistory"/>). Resolve <see cref="BlexHistory"/>
    /// to call <c>Undo()</c>/<c>Redo()</c>; <c>&lt;BlexProvider&gt;</c> starts recording automatically.
    /// </summary>
    public static IServiceCollection AddBlexHistory(this IServiceCollection services, int maxEntries = 100)
    {
        services.AddScoped(sp => new BlexHistory(sp.GetRequiredService<BlexManager>(), maxEntries));
        return services;
    }
}
