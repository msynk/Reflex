# Reflex

Lightweight, source-generator-powered **reactive state management for Blazor** - with **Redux DevTools time-travel** built in.

Reflex fills a real gap in the Blazor ecosystem. Fluxor is the de-facto Redux library but is widely criticized for boilerplate (separate Action / Reducer / Effect / Feature classes per operation) and for having no first-class DevTools time-travel. Reflex keeps the good parts of the Flux model - a single observable state tree, named actions, middleware - while a Roslyn source generator removes the ceremony and a tiny JS bridge wires you straight into the Redux DevTools browser extension.

## Why Reflex

| | Fluxor | Reflex |
|---|---|---|
| Define a piece of state | Feature + State class | one `[State]` field |
| Define an action | Action class + Reducer method | one `[Action]` method |
| Derived state | manual / selectors | `[Computed]` (memoized) |
| Middleware | yes | yes |
| Redux DevTools time-travel | ✗ | ✓ built in |
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

## Time-travel debugging

1. Install the [Redux DevTools](https://github.com/reduxjs/redux-devtools) browser extension.
2. Run the app and open DevTools - you'll see an instance named after `DevToolsName`.
3. Every action streams in with the resulting state tree.
4. Use the slider / jump buttons to rewind and replay your application state live.

Under the hood the `Reflex.Blazor` JS bridge talks to `window.__REDUX_DEVTOOLS_EXTENSION__`,
sends each action via `send(action, state)`, and applies `JUMP_TO_STATE` / `JUMP_TO_ACTION` /
`ROLLBACK` / `RESET` / `COMMIT` messages back onto the stores.

## Projects

| Project | Description |
|---|---|
| `src/Reflex` | Core runtime (no JS dependency): `StoreBase`, attributes, dispatch, middleware, `ReflexManager` manager. |
| `src/Reflex.Generators` | Roslyn incremental source generator. |
| `src/Reflex.Blazor` | Blazor integration: `ReflexComponentBase`, `<ReflexProvider>`, Redux DevTools bridge. |
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
