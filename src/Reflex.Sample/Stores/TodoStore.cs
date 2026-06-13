using System.Collections.Immutable;
using Reflex;

namespace Reflex.Sample.Stores;

/// <summary>A todo store demonstrating collection state, computed counts and an async action.</summary>
[Store(Name = "todos")]
public partial class TodoStore
{
    [State] private ImmutableList<TodoItem> _items = ImmutableList<TodoItem>.Empty;
    [State] private bool _loading;

    [Computed]
    private int ComputeRemaining() => Items.Count(i => !i.Done);

    [Computed]
    private int ComputeTotal() => Items.Count;

    [Action]
    private void OnAdd(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
            Items = Items.Add(new TodoItem(Guid.NewGuid(), text.Trim(), false));
    }

    [Action]
    private void OnToggle(Guid id)
        => Items = Items.Select(i => i.Id == id ? i with { Done = !i.Done } : i).ToImmutableList();

    [Action]
    private void OnRemove(Guid id)
        => Items = Items.RemoveAll(i => i.Id == id);

    [Action]
    private void OnClearCompleted()
        => Items = Items.RemoveAll(i => i.Done);

    [Action(Name = "SeedSampleData")]
    private async Task OnSeedAsync()
    {
        Loading = true;
        await Task.Delay(400); // pretend to call an API
        Items = ImmutableList.Create(
            new TodoItem(Guid.NewGuid(), "Try Reflex time-travel in DevTools", false),
            new TodoItem(Guid.NewGuid(), "Star the repo", false),
            new TodoItem(Guid.NewGuid(), "Delete Fluxor boilerplate", true));
        Loading = false;
    }
}

/// <summary>An immutable todo item. Records serialize cleanly for snapshots and DevTools.</summary>
public sealed record TodoItem(Guid Id, string Text, bool Done);
