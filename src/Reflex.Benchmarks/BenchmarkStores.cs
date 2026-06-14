using Reflex;

namespace Reflex.Benchmarks;

// Stores used by the benchmarks. Decorated like any real store so the source generator emits the
// actual reactive API the benchmarks exercise.

[Store(Name = "benchCounter")]
public partial class BenchCounterStore
{
    [State] private long _count;
    [State] private string _label = "idle";

    [Computed]
    private long ComputeDoubleCount() => Count * 2;

    [Action]
    private void OnIncrement() => Count++;

    [Action]
    private void OnBump(long amount)
    {
        Count += amount;
        Label = "bumped";
    }

    [Action(Name = "LoadFast")]
    private async Task OnLoadFast()
    {
        Label = "loading";
        await Task.Yield();
        Count++;
        Label = "loaded";
    }
}

// A wider store so global-state capture and serialization have realistic work to do.
[Store(Name = "benchWide")]
public partial class BenchWideStore
{
    [State] private int _a;
    [State] private int _b;
    [State] private int _c;
    [State] private string _title = "";
    [State] private bool _flag;
    [State] private double _ratio;

    [Action]
    private void OnTouch(int seed)
    {
        A = seed;
        B = seed + 1;
        C = seed + 2;
        Title = "row-" + seed;
        Flag = (seed & 1) == 0;
        Ratio = seed / 3.0;
    }
}
