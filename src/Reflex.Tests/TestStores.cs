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

[Store(Name = "settings", Persist = true)]
public partial class SettingsStore
{
    [State] private string _theme = "light";
    [State] private int _fontSize = 14;

    [Action]
    private void OnSetTheme(string theme) => Theme = theme;
}

[Store(Name = "data")]
public partial class DataStore
{
    [State] private string _value = "";
    public bool ShouldThrow { get; set; }

    [Effect]
    private async Task OnLoad(string input)
    {
        await Task.Delay(1);
        if (ShouldThrow)
            throw new InvalidOperationException("boom");
        Value = input;
    }
}

public record Todo(int Id, string Text, bool Done);

[Store(Name = "todos")]
public partial class EntityTodoStore
{
    private static readonly EntityAdapter<Todo, int> Adapter = new(t => t.Id);

    [State] private EntityState<Todo, int> _todos = Adapter.GetInitialState();

    [Computed] private int ComputeRemaining()
    {
        var n = 0;
        foreach (var t in Todos.All)
            if (!t.Done) n++;
        return n;
    }

    [Action] private void OnAdd(Todo todo) => Todos = Adapter.UpsertOne(Todos, todo);

    [Action] private void OnToggle(int id) => Todos = Adapter.UpdateOne(Todos, id, t => t with { Done = !t.Done });

    [Action] private void OnRemove(int id) => Todos = Adapter.RemoveOne(Todos, id);
}
