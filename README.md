# Reflex

Lightweight, source-generator-powered **reactive state management for Blazor** - with **Redux DevTools time-travel** built in.

Reflex fills a real gap in the Blazor ecosystem. Fluxor is the de-facto Redux library but is widely criticized for boilerplate (separate Action / Reducer / Effect / Feature classes per operation) and for having no first-class DevTools time-travel. Reflex keeps the good parts of the Flux model - a single observable state tree, named actions, middleware - while a Roslyn source generator removes the ceremony and a tiny JS bridge wires you straight into the Redux DevTools browser extension.

## Why Reflex

| | Fluxor | Reflex |
|---|---|---|
| Define a piece of state | Feature + State class | one `[State]` field |
| Define an action | Action class + Reducer method | one `[Action]` method |
| Derived state | manual / selectors | `[Computed]` (memoized) |
| Async side-effects | Effect classes | `[Effect]` (auto loading/error) |
| Middleware | yes | yes (with veto/filter hooks) |
| Granular re-render | manual selectors | selector `Subscribe(...)` |
| Normalized collections | manual | `EntityAdapter` / `EntityState` |
| Persistence | manual | `[Store(Persist = true)]` + storage |
| Undo / redo | ✗ | `ReflexHistory` (in-app) |
| Redux DevTools time-travel | ✗ | ✓ built in (+ state/action sanitizers) |
| Test helpers | ✗ | `Reflex.Testing` harness |
| Boilerplate | high | minimal (generated) |

## The whole store

```csharp
[Store(Name = "counter")]
public partial class CounterStore
{
    [State] private int _count;
    [State] private int _step = 1;

    [Computed] private int  ComputeDoubleCount() => Count * 2;
    [Computed] private bool ComputeIsEven()      => Count % 2 == 0;

    [Action] private void OnIncrement()       => Count += Step;
    [Action] private void OnSetStep(int step) => Step = step;
    [Action] private void OnReset()           { Count = 0; Step = 1; }
}
```

The generator emits the reactive `Count`/`Step` properties, the memoized `DoubleCount`/`IsEven`
accessors, the public `Increment()`/`SetStep(int)`/`Reset()` action wrappers, JSON snapshot
support and the `StoreBase` base type.

### Conventions

- **State**: `[State] private T _foo;` → public reactive property `Foo`.
- **Computed**: `[Computed]` on a parameterless `ComputeXxx()` / `GetXxx()` method → memoized property `Xxx`,
  automatically invalidated whenever state changes.
- **Actions**: `[Action]` on a method named `OnXxx` → public `Xxx(...)` wrapper that batches all the
  mutations inside it into a single, named, time-travel-recorded action. `async Task` methods are
  supported (they update the UI as they go but record as one action). Override the name with
  `[Action(Name = "...")]`.
- Directly assigning a generated property (e.g. `store.Count = 5`) is recorded as a `Set Count` action.

## Setup

```csharp
// Program.cs
builder.Services.AddReflex(options =>
{
    options.DevToolsName = "My App";
    options.UseMiddleware(ctx => Console.WriteLine($"[reflex] {ctx.QualifiedName}"));
});
builder.Services.AddReflexStore<CounterStore>();
builder.Services.AddReflexStore<TodoStore>();
```

```razor
@* App.razor - wrap your router once *@
<ReflexProvider>
    <Router ... />
</ReflexProvider>
```

```razor
@* Counter.razor *@
@inherits ReflexComponentBase
@inject CounterStore Store

<p>Count: @Store.Count (double: @Store.DoubleCount)</p>
<button @onclick="Store.Increment">+@Store.Step</button>

@code {
    protected override void OnInitialized() => Subscribe(Store);
}
```

`ReflexComponentBase.Subscribe(...)` re-renders the component whenever a subscribed store changes
and unsubscribes automatically on dispose.

## Granular subscriptions (selectors)

`Subscribe(store)` re-renders on any change to that store. For stores with many independent fields,
subscribe to a projection instead so unrelated changes don't re-render the component:

```csharp
protected override void OnInitialized()
    => Subscribe(Store, () => Store.Count); // re-renders only when Count changes
```

The same primitive is available outside Blazor:

```csharp
using var sub = store.Subscribe(() => store.Count, count => Console.WriteLine(count));
```

## Effects (async with managed loading/error)

`[Effect]` marks an async method (returning `Task`/`ValueTask`) whose loading and error lifecycle is
generated for you. The body is still recorded as a single, named, time-travelable action.

```csharp
[Effect]
private async Task OnLoadUser(int id)
{
    var user = await _api.GetUserAsync(id);
    User = user;
}
```

The generator emits `LoadUser(int)` plus reactive `LoadUserIsLoading` (bool) and `LoadUserError`
(`Exception?`) properties. The wrapper sets `IsLoading` while running and captures any thrown
exception into `Error` instead of propagating it.

## Normalized collections (entity adapter)

`EntityAdapter<TEntity, TKey>` generates CRUD operations over an immutable, id-keyed
`EntityState<TEntity, TKey>` - the same idea as Redux Toolkit's `createEntityAdapter`.

```csharp
[Store(Name = "todos")]
public partial class TodoStore
{
    private static readonly EntityAdapter<Todo, int> Adapter = new(t => t.Id);
    [State] private EntityState<Todo, int> _todos = Adapter.GetInitialState();

    [Computed] private int ComputeRemaining() => Todos.All.Count(t => !t.Done);

    [Action] private void OnUpsert(Todo todo) => Todos = Adapter.UpsertOne(Todos, todo);
    [Action] private void OnToggle(int id)    => Todos = Adapter.UpdateOne(Todos, id, t => t with { Done = !t.Done });
    [Action] private void OnRemove(int id)    => Todos = Adapter.RemoveOne(Todos, id);
}
```

`EntityState` exposes `Ids`, `Entities`, `All`, `Count`, `Contains(id)` and `Find(id)`, and
round-trips through JSON for snapshots and persistence.

## Persistence

Mark a store with `[Store(Persist = true)]` and wire up a storage provider; the store is rehydrated
on startup and saved after every action.

```csharp
[Store(Name = "settings", Persist = true)]
public partial class SettingsStore { [State] private string _theme = "light"; ... }
```

```csharp
// Program.cs (Blazor WebAssembly)
builder.Services.AddReflexLocalStoragePersistence();   // or AddReflexSessionStoragePersistence()
```

`<ReflexProvider>` restores persisted state on init. It also bridges to Blazor's
`PersistentComponentState` automatically (set `PersistComponentState="false"` to opt out), handing
prerendered state to the interactive render to avoid the prerender "double render" flicker.
For non-Blazor hosts, implement `IReflexStorage` and call `AddReflexPersistence()`.

## Cross-store coordination

React to one store's actions from elsewhere (e.g. trigger an effect on another store):

```csharp
manager.SubscribeTo<CounterStore>(ctx => { /* runs after each CounterStore action */ });
manager.SubscribeToAction("Increment", ctx => { ... });
manager.SubscribeAsync(async ctx => await otherStore.Reload());
```

## Middleware: observe and veto

Middleware sees every action after it applies, and can veto an action before it runs:

```csharp
builder.Services.AddReflex(options =>
{
    options.UseMiddleware(ctx => Console.WriteLine(ctx.QualifiedName)); // observe
    options.UseFilter(ctx => !IsReadOnly);                              // return false to cancel
});
```

## Undo / redo

`ReflexHistory` provides in-app undo/redo over the whole application state, independent of the
DevTools extension:

```csharp
builder.Services.AddReflexHistory();   // <ReflexProvider> starts recording automatically
```

```razor
@inject ReflexHistory History
<button @onclick="History.Undo" disabled="@(!History.CanUndo)">Undo</button>
<button @onclick="History.Redo" disabled="@(!History.CanRedo)">Redo</button>
```

## Testing

`Reflex.Testing` provides a zero-setup harness that records dispatched actions:

```csharp
using var harness = ReflexTestHarness.For<CounterStore>();
harness.Store.Increment();
Assert.Equal(new[] { "Increment" }, harness.Log.Names);
Assert.Equal(1, harness.Snapshot()["Count"]!.GetValue<int>());
```

## Time-travel debugging

1. Install the [Redux DevTools](https://github.com/reduxjs/redux-devtools) browser extension.
2. Run the app and open DevTools - you'll see an instance named after `DevToolsName`.
3. Every action streams in with the resulting state tree.
4. Use the slider / jump buttons to rewind and replay your application state live.

Under the hood the `Reflex.Blazor` JS bridge talks to `window.__REDUX_DEVTOOLS_EXTENSION__`,
sends each action via `send(action, state)`, and applies `JUMP_TO_STATE` / `JUMP_TO_ACTION` /
`ROLLBACK` / `RESET` / `COMMIT` messages back onto the stores.

For production, set `<ReflexProvider EnableDevTools="false">` to disable the connection entirely, or
redact sensitive values from the monitor with sanitizers:

```csharp
builder.Services.AddReflex(options =>
{
    options.RedactDevToolsKeys("token", "password");      // replace matching keys with <redacted>
    options.DevToolsActionSanitizer = label => label;     // or rewrite action labels
});
```

## Projects

| Project | Description |
|---|---|
| `src/Reflex` | Core runtime (no JS dependency): `StoreBase`, attributes, dispatch, middleware, persistence, entity adapter, undo/redo, `ReflexManager` manager. |
| `src/Reflex.Generators` | Roslyn incremental source generator. |
| `src/Reflex.Blazor` | Blazor integration: `ReflexComponentBase`, `<ReflexProvider>`, browser-storage persistence, Redux DevTools bridge. |
| `src/Reflex.Testing` | Test harness and assertions (`ReflexTestHarness`, `ActionLog`). |
| `src/Reflex.Sample` | Blazor WebAssembly demo (Counter + Todos). |
| `src/Reflex.Tests` | xUnit tests for the runtime and generated code. |

When consumed as a NuGet package, referencing `Reflex` brings the generator automatically
(it is packed into `analyzers/dotnet/cs`). Inside this repo the sample/tests reference the
generator project directly as an analyzer.

## Build & test

```bash
dotnet build src/Reflex.slnx
dotnet test src/Reflex.slnx
dotnet run --project src/Reflex.Sample
```

## License

MIT
