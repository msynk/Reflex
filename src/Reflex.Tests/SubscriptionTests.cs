using Reflex;
using Xunit;

namespace Reflex.Tests;

public class SubscriptionTests
{
    [Fact]
    public void SelectorSubscription_FiresOnlyWhenSelectedValueChanges()
    {
        var store = new CounterStore();
        var fires = 0;
        var lastValue = 0;
        using var sub = store.Subscribe(() => store.Count, v => { fires++; lastValue = v; });

        store.Label = "changed"; // unrelated field -> no fire
        Assert.Equal(0, fires);

        store.Increment(); // Count changed -> fire
        Assert.Equal(1, fires);
        Assert.Equal(1, lastValue);
    }

    [Fact]
    public void SelectorSubscription_NoFire_WhenSelectedValueUnchanged()
    {
        var store = new CounterStore();
        var fires = 0;
        using var sub = store.Subscribe(() => store.Count, _ => fires++);

        store.Label = "a";
        store.Label = "b";

        Assert.Equal(0, fires);
    }

    [Fact]
    public void SelectorSubscription_Dispose_StopsObserving()
    {
        var store = new CounterStore();
        var fires = 0;
        var sub = store.Subscribe(() => store.Count, _ => fires++);

        store.Increment();
        Assert.Equal(1, fires);

        sub.Dispose();
        store.Increment();
        Assert.Equal(1, fires); // no further fires after dispose
    }

    [Fact]
    public void SelectorSubscription_WorksWithComputedProjection()
    {
        var store = new CounterStore();
        var fires = 0;
        using var sub = store.Subscribe(() => store.DoubleCount, _ => fires++);

        store.Increment();
        Assert.Equal(1, fires);
        Assert.Equal(2, store.DoubleCount);
    }
}
