using Blex;

namespace Blex.Sample.Stores;

/// <summary>
/// A tiny counter store showing reactive state, a computed value and named actions.
/// Everything below the fields is generated: the <c>Count</c>/<c>Step</c> properties,
/// the memoized <c>IsEven</c>/<c>DoubleCount</c> values and the <c>Increment</c>/<c>Decrement</c>/
/// <c>SetStep</c>/<c>Reset</c> action wrappers.
/// </summary>
[Store(Name = "counter", Persist = true)]
public partial class CounterStore
{
    [State] private int _count;
    [State] private int _step = 1;

    [Computed]
    private int ComputeDoubleCount() => Count * 2;

    [Computed]
    private bool ComputeIsEven() => Count % 2 == 0;

    [Action]
    private void OnIncrement() => Count += Step;

    [Action]
    private void OnDecrement() => Count -= Step;

    [Action]
    private void OnSetStep(int step) => Step = step;

    [Action]
    private void OnReset()
    {
        Count = 0;
        Step = 1;
    }
}
