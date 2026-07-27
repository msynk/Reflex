# Reflex

Lightweight, source-generator-powered **reactive state management for Blazor** - with **Redux DevTools time-travel** built in.

Reflex fills a real gap in the Blazor ecosystem. Fluxor is the de-facto Redux library but is widely criticized for boilerplate (separate Action / Reducer / Effect / Feature classes per operation) and for having no first-class DevTools time-travel. Reflex keeps the good parts of the Flux model - a single observable state tree, named actions, middleware - while a Roslyn source generator removes the ceremony and a tiny JS bridge wires you straight into the Redux DevTools browser extension.

## Why Reflex

| | Fluxor | Reflex |
|---|---|---|
| Define a piece of state | Feature + State class | one `[State]` field |
| Define an action | Action class + Reducer method | one `[Action]` method |
| Action payloads in DevTools/middleware | manual | automatic (`ctx.Args`, DevTools payload) |
| Derived state | manual / selectors | `[Computed]` (memoized) |
| Async side-effects | Effect classes | `[Effect]` (auto loading/error) |
| Effect cancellation / concurrency | manual | `CancellationToken` + `Latest`/`Drop`/`Queue` modes |
| Ad-hoc batched mutations | ✗ | `store.Batch(name, ...)` (Pinia `$patch`-style) |
| Reset to initial state | manual | `store.ResetState()` |
| Middleware | yes | yes (with veto/filter hooks + payload access) |
| Granular re-render | manual selectors | selector `Subscribe(...)` (+ prev/current, fireImmediately) |
| Normalized collections | manual | `EntityAdapter` / `EntityState` (+ sorting, `UpdateMany`, `Map`) |
| Persistence | 3rd-party | `[Store(Persist = true)]` + debounce + versioning/migrations |
| Undo / redo | ✗ | `ReflexHistory` (in-app, labeled entries) |
| Redux DevTools time-travel | ✗ | ✓ built in (+ state/action sanitizers) |
| Error isolation hook | ✗ | `options.OnError` |
| Test helpers | ✗ | `Reflex.Testing` harness (+ `WaitForAsync`) |
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
  `[Action(Name = "...")]`. Action arguments are captured as the action's payload (visible to
  middleware, subscribers and DevTools).
- Directly assigning a generated property (e.g. `store.Count = 5`) is recorded as a `Set Count` action.
- **Batching from outside**: `store.Batch("Apply preset", () => { store.Count = 10; store.Step = 5; })`
  groups ad-hoc mutations into one named action with a single re-render (the `$patch`/`runInAction`
  equivalent).
- **Reset**: `store.ResetState()` returns the store to the state it had when first registered,
  recorded as a normal, vetoable `ResetState` action.

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

The same primitive is available outside Blazor, with optional previous-value delivery (MobX
`reaction`-style) and `fireImmediately`:

```csharp
using var sub = store.Subscribe(() => store.Count, count => Console.WriteLine(count));
using var log = store.Subscribe(() => store.Count,
    (prev, curr) => Console.WriteLine($"{prev} -> {curr}"), fireImmediately: true);
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
(`Exception?`) properties. The wrapper keeps `IsLoading` true while any run is in flight (overlapping
runs are reference-counted) and captures any thrown exception into `Error` instead of propagating it.

### Cancellation and concurrency

Give the effect a trailing `CancellationToken` parameter and the generator supplies the token and
emits a `CancelXxx()` method. `Concurrency` selects how overlapping invocations behave, mirroring
the RxJS flattening operators used by NgRx effects:

```csharp
[Effect(Concurrency = EffectConcurrency.Latest)]   // switchMap: new call cancels the previous
private async Task OnSearch(string query, CancellationToken ct)
{
    Results = await _api.SearchAsync(query, ct);
}
// generated: Task Search(string query)  +  void CancelSearch()
//            bool SearchIsLoading       +  Exception? SearchError
```

| Mode | Semantics | Typical use |
|---|---|---|
| `Parallel` (default) | all runs proceed concurrently | independent fetches |
| `Latest` | new run cancels the previous (`switchMap`) | type-ahead search |
| `Drop` | ignored while one is running (`exhaustMap`) | double-click-proof submits |
| `Queue` | runs strictly in arrival order (`concatMap`) | ordered writes |

Cancellation through the effect's own token (via `CancelXxx()` or `Latest` supersession) is a
normal outcome and never populates `Error`. A *foreign* `OperationCanceledException` — an
`HttpClient` timeout, or any cancellation when the effect has no token parameter — is a real
failure and is recorded in `Error`.

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
round-trips through JSON for snapshots and persistence. The adapter also offers `AddMany`,
`UpsertMany`, `UpdateMany`, `Map` (transform every entity), `RemoveMany`, `RemoveAll` and `SetAll`,
plus an optional sort comparer that keeps `Ids` ordered after every operation:

```csharp
private static readonly EntityAdapter<Todo, int> Adapter =
    new(t => t.Id, Comparer<Todo>.Create((a, b) => a.DueDate.CompareTo(b.DueDate)));
```

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
prerendered state to the interactive render to avoid the prerender "double render" flicker. Under
Blazor Server prerendering (where JS interop is unavailable), hydration is automatically retried on
first render instead of crashing startup. For non-Blazor hosts, implement `IReflexStorage` and call
`AddReflexPersistence()`.

Persistence is production-hardened:

- **Corrupt data never breaks startup** — an unreadable payload is reported through `OnError`,
  discarded, and removed from storage.
- **Debounce** — `options.DebounceInterval = TimeSpan.FromMilliseconds(300)` coalesces bursts of
  actions into one write (flushed on dispose, or on demand via `persistor.FlushAsync()`).
- **Versioning & migrations** — bump `options.Version` when a persisted store's shape changes and
  supply `options.Migrate` to upgrade (or discard) old payloads, zustand-persist style:

```csharp
builder.Services.AddReflexLocalStoragePersistence(options =>
{
    options.Version = 2;
    options.Migrate = (storeName, fromVersion, state) =>
    {
        if (storeName == "settings" && fromVersion < 2)
            state["Theme"] = "system";   // rename/upgrade old values
        return state;                    // return null to discard instead
    };
});
```

- **Restore write-back** — undo/redo and DevTools time-travel write the restored state back to
  storage, so a reload never resurrects the pre-restore state.
- **Ordered writes** — saves are serialized in dispatch order; a stale payload can't overwrite a
  newer one.

## Cross-store coordination

React to one store's actions from elsewhere (e.g. trigger an effect on another store):

```csharp
manager.SubscribeTo<CounterStore>(ctx => { /* runs after each CounterStore action */ });
manager.SubscribeToAction("Increment", ctx => { ... });
manager.SubscribeAsync(async ctx => await otherStore.Reload());
```

## Middleware: observe and veto

Middleware sees every action after it applies (including its argument payload via `ctx.Args`), and
can veto an action before it runs — also based on the payload:

```csharp
builder.Services.AddReflex(options =>
{
    options.UseMiddleware(ctx => Console.WriteLine($"{ctx.QualifiedName}({string.Join(", ", ctx.Args)})"));
    options.UseFilter(ctx => !IsReadOnly);   // return false to cancel
    options.OnError = err => _logger.LogWarning(err.Exception, "[reflex:{Source}] {Detail}", err.Source, err.Detail);
});
```

`OnError` receives every non-fatal failure Reflex isolates from the dispatch pipeline (throwing
subscribers, middleware, persistence writes, restores) — without it they go to `Console.Error`.

## Undo / redo

`ReflexHistory` provides in-app undo/redo over the whole application state, independent of the
DevTools extension:

```csharp
builder.Services.AddReflexHistory();   // <ReflexProvider> starts recording automatically
```

```razor
@inject ReflexHistory History
<button @onclick="History.Undo" disabled="@(!History.CanUndo)">Undo @History.NextUndoLabel</button>
<button @onclick="History.Redo" disabled="@(!History.CanRedo)">Redo @History.NextRedoLabel</button>
```

`NextUndoLabel`/`NextRedoLabel` name the action about to be undone/redone (e.g. "Undo
counter/Increment"); `UndoCount`/`RedoCount` expose stack depths. When persistence is enabled,
undo/redo writes the restored state back to storage.

## Testing

`Reflex.Testing` provides a zero-setup harness that records dispatched actions:

```csharp
using var harness = ReflexTestHarness.For<CounterStore>();
harness.Store.Increment();
Assert.Equal(new[] { "Increment" }, harness.Log.Names);
Assert.Equal(1, harness.Snapshot()["Count"]!.GetValue<int>());

// Recorded actions include their argument payloads:
harness.Store.Add(5);
Assert.Equal(5, harness.Log.Last!.Args[0].Value);

// Await state conditions instead of sprinkling Task.Delay:
var load = harness.Store.LoadUser(42);
await harness.Store.WaitForAsync(() => !harness.Store.LoadUserIsLoading);
```

## Compile-time diagnostics

The generator validates store shapes and fails fast with precise errors instead of emitting broken
code: `REFLEX001` store not partial · `REFLEX002/003` underivable action/computed names ·
`REFLEX004` computed with parameters · `REFLEX005` generated-member collisions (including against
your own members and `StoreBase`) · `REFLEX006` nested/generic stores · `REFLEX007` non-async
effects · `REFLEX008` static/readonly members · `REFLEX009` `Latest` effect without a
`CancellationToken` (warning) · `REFLEX010` `async void` actions · `REFLEX011` discarded action
return values (warning) · `REFLEX012` state field/property name conflicts · `REFLEX013` by-ref
parameters · `REFLEX014` generic action/effect methods · `REFLEX015` record stores ·
`REFLEX016` conflicting base class.

## Time-travel debugging

1. Install the [Redux DevTools](https://github.com/reduxjs/redux-devtools) browser extension.
2. Run the app and open DevTools - you'll see an instance named after `DevToolsName`.
3. Every action streams in with its argument payload and the resulting state tree.
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
                                                          // (applies to action payloads too)
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
| `src/Reflex.Sample` | Blazor WebAssembly demo (Counter, Todos, Weather). |
| `src/Tests/Reflex.Tests` | xUnit tests for the runtime and generated code. |
| `src/Tests/Reflex.Generators.Tests` | Generator-driver tests: all REFLEX diagnostics + emission snapshots. |
| `src/Tests/Reflex.Benchmarks` | BenchmarkDotNet suites (dispatch, fan-out, serialization, entity adapter). |

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
