using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Blex.Blazor;

/// <summary>Blazor-specific DI helpers for wiring Blex persistence to browser storage.</summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Persists stores marked <c>[Store(Persist = true)]</c> to <c>window.localStorage</c>, surviving
    /// full page reloads. Call after <c>AddBlex</c> and the store registrations.
    /// </summary>
    public static IServiceCollection AddBlexLocalStoragePersistence(
        this IServiceCollection services,
        Action<BlexPersistenceOptions>? configure = null)
        => services.AddBlexBrowserPersistence(BrowserStorageKind.Local, configure);

    /// <summary>
    /// Persists stores marked <c>[Store(Persist = true)]</c> to <c>window.sessionStorage</c>, cleared
    /// when the tab closes. Call after <c>AddBlex</c> and the store registrations.
    /// </summary>
    public static IServiceCollection AddBlexSessionStoragePersistence(
        this IServiceCollection services,
        Action<BlexPersistenceOptions>? configure = null)
        => services.AddBlexBrowserPersistence(BrowserStorageKind.Session, configure);

    private static IServiceCollection AddBlexBrowserPersistence(
        this IServiceCollection services,
        BrowserStorageKind kind,
        Action<BlexPersistenceOptions>? configure)
    {
        services.AddScoped<IBlexStorage>(sp => new BrowserStorage(
            sp.GetRequiredService<IJSRuntime>(), kind));
        services.AddBlexPersistence(configure);
        return services;
    }
}
