using Blex;

namespace Blex.Benchmarks;

// Stores used by the benchmarks. Decorated like any real store so the source generator emits the
// actual reactive API the benchmarks exercise.

[StoreAttributeBlex(Name = "benchCounter")]
public partial class BenchCounterStore
{
    [StateAttributeBlex] private long _count;
    [StateAttributeBlex] private string _label = "idle";

    [ComputedAttributeBlex]
    private long ComputeDoubleCount() => Count * 2;

    [ActionAttributeBlex]
    private void OnIncrement() => Count++;

    [ActionAttributeBlex]
    private void OnBump(long amount)
    {
        Count += amount;
        Label = "bumped";
    }

    [ActionAttributeBlex(Name = "LoadFast")]
    private async Task OnLoadFast()
    {
        Label = "loading";
        await Task.Yield();
        Count++;
        Label = "loaded";
    }
}

// A wider store so global-state capture and serialization have realistic work to do.
[StoreAttributeBlex(Name = "benchWide")]
public partial class BenchWideStore
{
    [StateAttributeBlex] private int _a;
    [StateAttributeBlex] private int _b;
    [StateAttributeBlex] private int _c;
    [StateAttributeBlex] private string _title = "";
    [StateAttributeBlex] private bool _flag;
    [StateAttributeBlex] private double _ratio;

    [ActionAttributeBlex]
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
