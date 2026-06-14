using System.Collections.Generic;
using System.Threading.Tasks;
using Reflex;

namespace Reflex.Tests;

[Store(Name = "counter")]
public partial class CounterStore
{
    [State] private int _count;
    [State] private string _label = "idle";

    [Computed]
    private int ComputeDoubleCount() => Count * 2;

    private int _computeCalls;
    public int ComputeCalls => _computeCalls;

    [Computed]
    private int ComputeTrackedCount()
    {
        _computeCalls++;
        return Count + 1;
    }

    [Action]
    private void OnIncrement() => Count++;

    [Action]
    private void OnAdd(int amount) => Count += amount;

    [Action]
    private void OnReset()
    {
        Count = 0;
        Label = "idle";
    }

    [Action(Name = "LoadData")]
    private async Task OnLoadAsync()
    {
        Label = "loading";
        await Task.Delay(1);
        Count = 42;
        Label = "loaded";
    }

    // Returns ValueTask<T>: the generator must await it (via AsTask) rather than fire-and-forget.
    // The explicit name contains spaces, so it is a display label only; the wrapper is "LoadValue".
    [Action(Name = "Load Value")]
    private async ValueTask<int> OnLoadValue()
    {
        await Task.Delay(1);
        Count = 7;
        return Count;
    }
}

[Store]
public partial class TodoStore
{
    [State] private List<string> _items = new();

    [Action]
    private void OnAddItem(string text) => Items = new List<string>(Items) { text };
}
