namespace Blex.Demo.Stores;

/// <summary>
/// The canonical first store. Exercises the four building blocks -- <c>[State]</c>,
/// <c>[Computed]</c>, <c>[Action]</c> and an action with a payload -- and opts in to
/// persistence so a page reload keeps the value.
/// </summary>
[Store(Name = "counter", Persist = true)]
public partial class CounterStore
{
    [State] private int _count;
    [State] private int _step = 1;

    [Computed] private int ComputeDoubleCount() => Count * 2;

    [Computed] private bool ComputeIsEven() => Count % 2 == 0;

    [Action] private void OnIncrement() => Count += Step;

    [Action] private void OnDecrement() => Count -= Step;

    [Action] private void OnSetStep(int step) => Step = step;

    /// <summary>Two mutations inside one action: a single notification and one history entry.</summary>
    [Action(Name = "Apply preset")]
    private void OnApplyPreset(int count, int step)
    {
        Count = count;
        Step = step;
    }
}
