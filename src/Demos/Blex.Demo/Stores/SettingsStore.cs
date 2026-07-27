namespace Reflex.Demo.Stores;

/// <summary>UI density options for the settings demo.</summary>
public enum Density
{
    /// <summary>Roomy spacing.</summary>
    Comfortable,

    /// <summary>Tight spacing.</summary>
    Compact,
}

/// <summary>
/// A persisted store: <c>Persist = true</c> plus a registered <see cref="IReflexStorage"/> means
/// the state is rehydrated on startup and written back after every action. <c>Token</c> exists to
/// demonstrate DevTools redaction -- it is replaced with <c>&lt;redacted&gt;</c> in the monitor.
/// </summary>
[Store(Name = "settings", Persist = true)]
public partial class SettingsStore
{
    [State] private string _accent = "violet";
    [State] private Density _density = Density.Comfortable;
    [State] private bool _showLineNumbers = true;
    [State] private string _token = "sk-live-51H8xQ2eZvKYlo";

    [Computed] private string ComputeSummary() => $"{Accent} / {Density} / {(ShowLineNumbers ? "numbered" : "plain")}";

    [Action] private void OnSetAccent(string accent) => Accent = accent;

    [Action] private void OnSetDensity(Density density) => Density = density;

    [Action] private void OnToggleLineNumbers() => ShowLineNumbers = !ShowLineNumbers;

    [Action] private void OnRotateToken() => Token = "sk-live-" + Guid.NewGuid().ToString("N")[..14];
}
