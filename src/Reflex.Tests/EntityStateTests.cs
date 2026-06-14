using System.Linq;
using Reflex;
using Xunit;

namespace Reflex.Tests;

public class EntityStateTests
{
    private static readonly EntityAdapter<Todo, int> Adapter = new(t => t.Id);

    [Fact]
    public void AddOne_AppendsEntity()
    {
        var state = Adapter.GetInitialState();
        state = Adapter.AddOne(state, new Todo(1, "a", false));
        state = Adapter.AddOne(state, new Todo(2, "b", false));

        Assert.Equal(2, state.Count);
        Assert.Equal(new[] { 1, 2 }, state.Ids.ToArray());
        Assert.Equal("a", state.Find(1)!.Text);
    }

    [Fact]
    public void AddOne_IgnoresDuplicateId()
    {
        var state = Adapter.GetInitialState();
        state = Adapter.AddOne(state, new Todo(1, "a", false));
        state = Adapter.AddOne(state, new Todo(1, "dupe", false));

        Assert.Equal(1, state.Count);
        Assert.Equal("a", state.Find(1)!.Text); // original kept
    }

    [Fact]
    public void UpsertOne_AddsOrReplaces()
    {
        var state = Adapter.GetInitialState();
        state = Adapter.UpsertOne(state, new Todo(1, "a", false));
        state = Adapter.UpsertOne(state, new Todo(1, "updated", true));

        Assert.Equal(1, state.Count);
        Assert.Equal("updated", state.Find(1)!.Text);
        Assert.True(state.Find(1)!.Done);
    }

    [Fact]
    public void UpdateOne_MutatesViaUpdater()
    {
        var state = Adapter.GetInitialState();
        state = Adapter.AddOne(state, new Todo(1, "a", false));
        state = Adapter.UpdateOne(state, 1, t => t with { Done = true });

        Assert.True(state.Find(1)!.Done);
    }

    [Fact]
    public void RemoveOne_RemovesEntityAndId()
    {
        var state = Adapter.GetInitialState();
        state = Adapter.AddMany(state, new[] { new Todo(1, "a", false), new Todo(2, "b", false) });
        state = Adapter.RemoveOne(state, 1);

        Assert.Equal(1, state.Count);
        Assert.False(state.Contains(1));
        Assert.Equal(new[] { 2 }, state.Ids.ToArray());
    }

    [Fact]
    public void SetAll_ReplacesEverything()
    {
        var state = Adapter.GetInitialState();
        state = Adapter.AddOne(state, new Todo(99, "old", false));
        state = Adapter.SetAll(state, new[] { new Todo(1, "a", false), new Todo(2, "b", false) });

        Assert.Equal(new[] { 1, 2 }, state.Ids.ToArray());
    }

    [Fact]
    public void Mutations_ReturnNewInstances()
    {
        var state = Adapter.GetInitialState();
        var next = Adapter.AddOne(state, new Todo(1, "a", false));
        Assert.NotSame(state, next); // immutability => change detection works
    }

    [Fact]
    public void EntityStore_IntegratesWithActionsAndComputed()
    {
        var store = new EntityTodoStore();
        store.Add(new Todo(1, "a", false));
        store.Add(new Todo(2, "b", false));
        Assert.Equal(2, store.Remaining);

        store.Toggle(1);
        Assert.Equal(1, store.Remaining);

        store.Remove(2);
        Assert.Equal(1, store.Todos.Count);
    }

    [Fact]
    public void EntityStore_SerializesAndRoundTrips()
    {
        var store = new EntityTodoStore();
        store.Add(new Todo(1, "a", false));
        store.Add(new Todo(2, "b", true));

        var json = store.SerializeState();
        var restored = new EntityTodoStore();
        restored.DeserializeState(json);

        Assert.Equal(2, restored.Todos.Count);
        Assert.Equal(new[] { 1, 2 }, restored.Todos.Ids.ToArray());
        Assert.True(restored.Todos.Find(2)!.Done);
    }
}
