using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;

namespace Blex.Maui;

/// <summary>
/// Runs when <c>MauiApp.Build()</c> executes, performing the startup work that
/// <c>&lt;BlexProvider&gt;</c> does in Blazor hosts: register every store with the manager,
/// rehydrate persisted state, and start undo/redo recording.
/// </summary>
internal sealed class BlexMauiInitializer : IMauiInitializeService
{
    public void Initialize(IServiceProvider services)
    {
        // Resolving the stores is what attaches them to the manager (see AddBlexStore); do it
        // before hydration so every persistent store is known to the persistor.
        _ = services.GetServices<IStore>();

        var persistor = services.GetService<StatePersistor>();
        if (persistor is not null)
        {
            try
            {
                // PreferencesBlexStorage completes synchronously, so this never actually blocks.
                // A custom asynchronous IBlexStorage should be hydrated from app code instead
                // (await persistor.StartAsync()) rather than rely on this synchronous wait.
                persistor.StartAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                // Unreadable storage must not crash app startup: report through OnError and run
                // with default state, mirroring <BlexProvider>'s containment.
                services.GetRequiredService<BlexManager>().ReportError("persistence", ex, "hydrating at startup");
            }
        }

        // Start recording history only after rehydration so the baseline (the state Undo
        // ultimately returns to) is the hydrated state, not the pre-hydration defaults.
        services.GetService<BlexHistory>()?.Start();
    }
}
