namespace Blex.Demo.Stores;

/// <summary>
/// The canonical first store. Exercises the four building blocks -- <c>[StateAttributeBlex]</c>,
/// <c>[ComputedAttributeBlex]</c>, <c>[ActionAttributeBlex]</c> and an action with a payload -- and opts in to
/// persistence so a page reload keeps the value.
/// </summary>
[StoreAttributeBlex(Name = "counter", Persist = true)]
public partial class CounterStore
{
    [StateAttributeBlex] private int _count;
    [StateAttributeBlex] private int _step = 1;

    [ComputedAttributeBlex] private int ComputeDoubleCount() => Count * 2;

    [ComputedAttributeBlex] private bool ComputeIsEven() => Count % 2 == 0;

    [ActionAttributeBlex] private void OnIncrement() => Count += Step;

    [ActionAttributeBlex] private void OnDecrement() => Count -= Step;

    [ActionAttributeBlex] private void OnSetStep(int step) => Step = step;

    /// <summary>Two mutations inside one action: a single notification and one history entry.</summary>
    [ActionAttributeBlex(Name = "Apply preset")]
    private void OnApplyPreset(int count, int step)
    {
        Count = count;
        Step = step;
    }
}
