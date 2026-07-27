using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Reflex.Blazor;

/// <summary>Blazor-specific DI helpers for wiring Reflex persistence to browser storage.</summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Persists stores marked <c>[Store(Persist = true)]</c> to <c>window.localStorage</c>, surviving
    /// full page reloads. Call after <c>AddReflex</c> and the store registrations.
    /// </summary>
    public static IServiceCollection AddReflexLocalStoragePersistence(
        this IServiceCollection services,
        Action<ReflexPersistenceOptions>? configure = null)
        => services.AddReflexBrowserPersistence(BrowserStorageKind.Local, configure);

    /// <summary>
    /// Persists stores marked <c>[Store(Persist = true)]</c> to <c>window.sessionStorage</c>, cleared
    /// when the tab closes. Call after <c>AddReflex</c> and the store registrations.
    /// </summary>
    public static IServiceCollection AddReflexSessionStoragePersistence(
        this IServiceCollection services,
        Action<ReflexPersistenceOptions>? configure = null)
        => services.AddReflexBrowserPersistence(BrowserStorageKind.Session, configure);

    private static IServiceCollection AddReflexBrowserPersistence(
        this IServiceCollection services,
        BrowserStorageKind kind,
        Action<ReflexPersistenceOptions>? configure)
    {
        services.AddScoped<IReflexStorage>(sp => new BrowserStorage(
            sp.GetRequiredService<IJSRuntime>(), kind));
        services.AddReflexPersistence(configure);
        return services;
    }
}
