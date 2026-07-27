namespace Blex.Demo.Stores;

/// <summary>
/// A realistic type-ahead: <c>Latest</c> concurrency means each keystroke supersedes the
/// in-flight request, and the trailing <see cref="CancellationToken"/> makes the generator emit
/// <c>CancelSearch()</c>. The <c>SearchIsLoading</c> / <c>SearchError</c> properties come for free.
/// </summary>
[Store(Name = "search")]
public partial class SearchStore
{
    private static readonly string[] Catalog =
    [
        "Blazor WebAssembly", "Blazor Server", "Blazor Hybrid", "Razor Components",
        "Source Generators", "Roslyn Analyzers", "Incremental Generators",
        "Redux DevTools", "Time-travel Debugging", "Entity Adapter",
        "State Management", "Dependency Injection", "Middleware Pipeline",
    ];

    [State] private string _query = "";
    [State] private IReadOnlyList<string> _results = [];
    [State] private bool _simulateFailure;

    [Computed] private bool ComputeHasResults() => Results.Count > 0;

    [Action] private void OnSetSimulateFailure(bool value) => SimulateFailure = value;

    [Effect(Concurrency = EffectConcurrency.Latest)]
    private async Task OnSearch(string query, CancellationToken ct)
    {
        Query = query;

        // A deliberately slow "network" call so supersession and cancellation are observable.
        await Task.Delay(700, ct);

        if (SimulateFailure)
            throw new InvalidOperationException($"The search service is unavailable (simulated) for '{query}'.");

        Results = string.IsNullOrWhiteSpace(query)
            ? []
            : [.. Catalog.Where(c => c.Contains(query, StringComparison.OrdinalIgnoreCase))];
    }

    [Action]
    private void OnClear()
    {
        Query = "";
        Results = [];
    }
}
