using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Blex.Blazor;

/// <summary>Blazor-specific DI helpers for wiring Blex persistence to browser storage.</summary>
public static class PersistenceServiceCollectionExtensionsBlex
{
    /// <summary>
    /// Persists stores marked <c>[StoreAttributeBlex(Persist = true)]</c> to <c>window.localStorage</c>, surviving
    /// full page reloads. Call after <c>AddBlex</c> and the store registrations.
    /// </summary>
    public static IServiceCollection AddBlexLocalStoragePersistence(
        this IServiceCollection services,
        Action<PersistenceOptionsBlex>? configure = null)
        => services.AddBlexBrowserPersistence(BrowserStorageKindBlex.Local, configure);

    /// <summary>
    /// Persists stores marked <c>[StoreAttributeBlex(Persist = true)]</c> to <c>window.sessionStorage</c>, cleared
    /// when the tab closes. Call after <c>AddBlex</c> and the store registrations.
    /// </summary>
    public static IServiceCollection AddBlexSessionStoragePersistence(
        this IServiceCollection services,
        Action<PersistenceOptionsBlex>? configure = null)
        => services.AddBlexBrowserPersistence(BrowserStorageKindBlex.Session, configure);

    private static IServiceCollection AddBlexBrowserPersistence(
        this IServiceCollection services,
        BrowserStorageKindBlex kind,
        Action<PersistenceOptionsBlex>? configure)
    {
        services.AddScoped<IStorageBlex>(sp => new BrowserStorageBlex(
            sp.GetRequiredService<IJSRuntime>(), kind));
        services.AddBlexPersistence(configure);
        return services;
    }
}
