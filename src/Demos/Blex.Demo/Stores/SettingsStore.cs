namespace Blex.Demo.Stores;

/// <summary>UI density options for the settings demo.</summary>
public enum Density
{
    /// <summary>Roomy spacing.</summary>
    Comfortable,

    /// <summary>Tight spacing.</summary>
    Compact,
}

/// <summary>
/// A persisted store: <c>Persist = true</c> plus a registered <see cref="IStorageBlex"/> means
/// the state is rehydrated on startup and written back after every action. <c>Token</c> exists to
/// demonstrate DevTools redaction -- it is replaced with <c>&lt;redacted&gt;</c> in the monitor.
/// </summary>
[StoreAttributeBlex(Name = "settings", Persist = true)]
public partial class SettingsStore
{
    [StateAttributeBlex] private string _accent = "violet";
    [StateAttributeBlex] private Density _density = Density.Comfortable;
    [StateAttributeBlex] private bool _showLineNumbers = true;
    [StateAttributeBlex] private string _token = "sk-live-51H8xQ2eZvKYlo";

    [ComputedAttributeBlex] private string ComputeSummary() => $"{Accent} / {Density} / {(ShowLineNumbers ? "numbered" : "plain")}";

    [ActionAttributeBlex] private void OnSetAccent(string accent) => Accent = accent;

    [ActionAttributeBlex] private void OnSetDensity(Density density) => Density = density;

    [ActionAttributeBlex] private void OnToggleLineNumbers() => ShowLineNumbers = !ShowLineNumbers;

    [ActionAttributeBlex] private void OnRotateToken() => Token = "sk-live-" + Guid.NewGuid().ToString("N")[..14];
}
