using System.Collections.Generic;
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
    public void SelectorSubscription_PreviousAndCurrent_AreDelivered()
    {
        var store = new CounterStore();
        var transitions = new List<(int From, int To)>();
        using var sub = store.Subscribe(() => store.Count, (prev, curr) => transitions.Add((prev, curr)));

        store.Increment();
        store.Add(4);

        Assert.Equal([(0, 1), (1, 5)], transitions);
    }

    [Fact]
    public void SelectorSubscription_FireImmediately_InvokesOnSubscribe()
    {
        var store = new CounterStore();
        store.Increment();

        var seen = new List<int>();
        using var sub = store.Subscribe(() => store.Count, seen.Add, fireImmediately: true);

        Assert.Equal([1], seen);
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
