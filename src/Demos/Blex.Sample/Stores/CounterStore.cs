using Blex;

namespace Blex.Sample.Stores;

/// <summary>
/// A tiny counter store showing reactive state, a computed value and named actions.
/// Everything below the fields is generated: the <c>Count</c>/<c>Step</c> properties,
/// the memoized <c>IsEven</c>/<c>DoubleCount</c> values and the <c>Increment</c>/<c>Decrement</c>/
/// <c>SetStep</c>/<c>Reset</c> action wrappers.
/// </summary>
[StoreAttributeBlex(Name = "counter", Persist = true)]
public partial class CounterStore
{
    [StateAttributeBlex] private int _count;
    [StateAttributeBlex] private int _step = 1;

    [ComputedAttributeBlex]
    private int ComputeDoubleCount() => Count * 2;

    [ComputedAttributeBlex]
    private bool ComputeIsEven() => Count % 2 == 0;

    [ActionAttributeBlex]
    private void OnIncrement() => Count += Step;

    [ActionAttributeBlex]
    private void OnDecrement() => Count -= Step;

    [ActionAttributeBlex]
    private void OnSetStep(int step) => Step = step;

    [ActionAttributeBlex]
    private void OnReset()
    {
        Count = 0;
        Step = 1;
    }
}
