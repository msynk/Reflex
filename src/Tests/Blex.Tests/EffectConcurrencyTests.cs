using System.Threading.Tasks;
using Blex.Testing;
using Xunit;

namespace Blex.Tests;

public class EffectConcurrencyTests
{
    private static TaskCompletionSource NewGate()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task Latest_CancelsPreviousRun_AndKeepsLastResult()
    {
        var store = new EffectConcurrencyStore();
        var gate1 = NewGate();
        var gate2 = NewGate();
        store.Gates.Enqueue(gate1);
        store.Gates.Enqueue(gate2);

        var first = store.Search("first");
        var second = store.Search("second"); // supersedes and cancels the first run

        gate2.SetResult();
        await second;
        gate1.SetResult(); // completing the old gate must have no effect
        await first;

        Assert.Equal("second", store.Last);
        Assert.Equal(1, store.Completed);
        Assert.Null(store.SearchError); // cancellation is not an error
        Assert.False(store.SearchIsLoading);
    }

    [Fact]
    public async Task CancelMethod_CancelsInFlightRun_WithoutError()
    {
        var store = new EffectConcurrencyStore();
        var gate = NewGate();
        store.Gates.Enqueue(gate);

        var run = store.Search("query");
        Assert.True(store.SearchIsLoading);

        store.CancelSearch();
        await run;

        Assert.False(store.SearchIsLoading);
        Assert.Null(store.SearchError);
        Assert.Equal(0, store.Completed);
    }

    [Fact]
    public async Task Drop_IgnoresInvocations_WhileOneIsRunning()
    {
        var store = new EffectConcurrencyStore();
        var gate = NewGate();
        store.Gates.Enqueue(gate);

        var first = store.Submit();
        var second = store.Submit(); // dropped: returns without running

        gate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, store.Completed);
    }

    [Fact]
    public async Task Queue_RunsInvocationsInOrder()
    {
        var store = new EffectConcurrencyStore();
        var gate1 = NewGate();
        var gate2 = NewGate();
        store.Gates.Enqueue(gate1);
        store.Gates.Enqueue(gate2);

        var first = store.Write("a");
        var second = store.Write("b"); // must wait for the first to finish

        // Complete the *second* gate first; the second run hasn't started its body yet,
        // so ordering must still be a then b.
        gate2.SetResult();
        Assert.Equal(0, store.Completed);

        gate1.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal("ab", store.Last);
        Assert.Equal(2, store.Completed);
    }

    [Fact]
    public async Task Parallel_OverlappingRuns_KeepLoadingUntilLastCompletes()
    {
        var store = new EffectConcurrencyStore();
        var gate1 = NewGate();
        var gate2 = NewGate();
        store.Gates.Enqueue(gate1);
        store.Gates.Enqueue(gate2);

        var first = store.Fetch();
        var second = store.Fetch();
        Assert.True(store.FetchIsLoading);

        gate1.SetResult();
        await first;
        Assert.True(store.FetchIsLoading); // second run still in flight

        gate2.SetResult();
        await second;
        Assert.False(store.FetchIsLoading);
        Assert.Equal(2, store.Completed);
    }

    [Fact]
    public async Task Latest_StaleRunFailure_DoesNotClobberNewestRunState()
    {
        var store = new EffectConcurrencyStore();
        var gate1 = NewGate();
        var gate2 = NewGate();
        store.Gates.Enqueue(gate1);
        store.Gates.Enqueue(gate2);

        var stale = store.Flaky(true);   // will fail once released
        var newest = store.Flaky(false); // supersedes

        gate2.SetResult();
        await newest;
        Assert.Equal("flaky-ok", store.Last);
        Assert.Null(store.FlakyError);

        gate1.SetResult();
        await stale; // the stale failure must be discarded

        Assert.Null(store.FlakyError);
        Assert.Equal("flaky-ok", store.Last);
        Assert.False(store.FlakyIsLoading);
    }

    [Fact]
    public async Task ForeignCancellation_IsRecordedAsError()
    {
        var store = new EffectConcurrencyStore();

        await store.Timeout(); // throws TaskCanceledException while our token is NOT cancelled

        Assert.IsType<TaskCanceledException>(store.TimeoutError);
        Assert.False(store.TimeoutIsLoading);
    }

    [Fact]
    public async Task Queue_FailedPredecessorError_IsClearedByASuccessfulSuccessor()
    {
        var store = new EffectConcurrencyStore();
        var gate1 = NewGate();
        store.Gates.Enqueue(gate1);

        // A lone failing run records its error.
        var loneFailure = store.QueuedFlaky(true);
        gate1.SetResult();
        await loneFailure;
        Assert.NotNull(store.QueuedFlakyError);

        // A failing run followed by a queued successful run: the successor clears the error
        // only after the predecessor finished, so "last run wins" holds regardless of timing.
        var gate3 = NewGate();
        var gate4 = NewGate();
        store.Gates.Enqueue(gate3);
        store.Gates.Enqueue(gate4);
        var failing = store.QueuedFlaky(true);
        var succeeding = store.QueuedFlaky(false);
        gate3.SetResult();
        await failing;
        gate4.SetResult();
        await succeeding;

        Assert.Null(store.QueuedFlakyError);
        Assert.Equal(1, store.Completed);
    }

    [Fact]
    public async Task WaitForAsync_AwaitsEffectCompletion()
    {
        var store = new EffectConcurrencyStore();
        var gate = NewGate();
        store.Gates.Enqueue(gate);

        var run = store.Fetch();
        var wait = store.WaitForAsync(() => !store.FetchIsLoading);

        gate.SetResult();
        await run;
        await wait; // must complete without timing out

        Assert.Equal(1, store.Completed);
    }
}
