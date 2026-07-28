using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Maui.Hosting;

namespace Blex.Maui;

/// <summary>DI helpers for wiring Blex into a .NET MAUI application.</summary>
public static class MauiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Blex manager and middleware (like <c>AddBlex</c>) plus the MAUI startup
    /// initializer that registers stores, rehydrates persisted state and starts history when the
    /// app is built. Call <c>AddBlexStore&lt;TStore&gt;()</c> for each store afterwards.
    /// </summary>
    /// <example><code>
    /// var builder = MauiApp.CreateBuilder();
    /// builder.UseBlex(options => options.DevToolsName = "My App");
    /// builder.Services.AddBlexStore&lt;CounterStore&gt;();
    /// builder.Services.AddBlexPreferencesPersistence();
    /// </code></example>
    public static MauiAppBuilder UseBlex(this MauiAppBuilder builder, Action<BlexOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddBlex(configure);
        builder.Services.AddBlexMauiInitializer();
        return builder;
    }

    /// <summary>
    /// Persists stores marked <c>[Store(Persist = true)]</c> to .NET MAUI
    /// <c>Preferences</c> (OS-native app settings). State is rehydrated automatically when
    /// <c>MauiApp.Build()</c> runs. Call after <c>UseBlex</c>/<c>AddBlex</c> and the store
    /// registrations.
    /// </summary>
    public static IServiceCollection AddBlexPreferencesPersistence(
        this IServiceCollection services,
        Action<BlexPersistenceOptions>? configure = null)
    {
        services.TryAddScoped<IBlexStorage, PreferencesBlexStorage>();
        services.AddBlexPersistence(configure);
        services.AddBlexMauiInitializer();
        return services;
    }

    /// <summary>
    /// Registers the startup initializer on its own -- useful when Blex was registered with the
    /// core <c>AddBlex</c> (rather than <see cref="UseBlex"/>) but store registration, hydration
    /// and history recording should still happen automatically at <c>MauiApp.Build()</c> time.
    /// Idempotent: calling it multiple times registers a single initializer.
    /// </summary>
    public static IServiceCollection AddBlexMauiInitializer(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMauiInitializeService, BlexMauiInitializer>());
        return services;
    }
}
