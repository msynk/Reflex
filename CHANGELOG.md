# Changelog

All notable changes to Blex are documented here.

## [Unreleased]

## [0.3.0] - 2026-07-27

First release published to NuGet.

### Packaging

- **Multi-targeting**: `Blex`, `Blex.Blazor` and `Blex.Testing` now build and ship for
  **net8.0, net9.0 and net10.0**, each with framework dependencies matched to its own band. The
  full runtime test suite runs against all three.
- `System.Threading.Lock` (net9+) is aliased to the classic monitor object on net8.0, so lock
  sites compile unchanged on the older framework.
- Packages carry a readme, icon, MIT license expression, repository metadata, tags, symbol
  packages (`.snupkg`) and Source Link, and are produced deterministically. Metadata lives in
  `src/Directory.Build.props`; `dotnet pack src/Blex.slnx -c Release` writes to
  `artifacts/packages`.
- `Blex.Generators` is no longer a separate package. It is bundled into `Blex` under
  `analyzers/dotnet/cs`, and `Blex.Blazor`/`Blex.Testing` depend on `Blex` with
  `PrivateAssets="none"` so the generator reaches consumers that reference only those packages.
- CI packs on every run; a `Release` workflow publishes to nuget.org from a `v*` tag after
  checking that the tag matches `<Version>`.

### Added

- **Action payloads**: generated action/effect wrappers capture their arguments as `ActionArg`s,
  visible on `BlexActionContext.Args` / `BlexPreActionContext.Args` (payload-aware veto
  filters), in `Blex.Testing`'s `ActionLog`, and as the `payload` of the action objects streamed
  to Redux DevTools. Key redaction (`RedactDevToolsKeys`) applies to payloads too.
- **Effect cancellation & concurrency**: an effect whose last parameter is a `CancellationToken`
  gets the token supplied by the generated wrapper plus a `CancelXxx()` method.
  `[Effect(Concurrency = ...)]` supports `Parallel` (default), `Latest` (switchMap), `Drop`
  (exhaustMap) and `Queue` (concatMap). Cancellations never populate `XxxError`, and overlapping
  runs are reference-counted so `XxxIsLoading` stays true until the last run finishes.
- **`store.Batch(name, mutations)`**: group ad-hoc mutations from outside the store into one
  named, vetoable, time-travelable action with a single re-render (Pinia `$patch` equivalent).
- **`store.ResetState()`**: return a store to the state it had when first registered, recorded as
  a normal action. **`store.RestoreState(snapshot)`**: safe public hydration entry point (notifies
  and invalidates computeds, unlike raw `DeserializeState`).
- **Persistence hardening**: corrupt payloads are reported, discarded and removed instead of
  crashing startup; `DebounceInterval` coalesces write bursts; `Version` + `Migrate` provide
  zustand-style versioned migrations; writes are serialized in dispatch order; `FlushAsync()` and
  flush-on-dispose; undo/redo/time-travel restores are written back to storage
  (`BlexManager.StateRestored`).
- **Error hook**: `options.OnError` / `BlexManager.OnError` receives every isolated pipeline
  failure (`BlexError` with source, exception, detail) instead of ad-hoc `Console.Error` writes.
- **Selector subscriptions**: `(previous, current)` overload and `fireImmediately` option.
- **History**: `UndoCount`/`RedoCount` and `NextUndoLabel`/`NextRedoLabel` (per-action labels for
  "Undo Increment"-style UI).
- **Entity adapter**: optional `sortComparer` constructor (keeps `Ids` sorted through every
  operation), `UpdateMany`, `Map`.
- **Testing**: `store.WaitForAsync(condition, timeout)`; recorded actions expose `Args`.
- **Manager**: `Unregister(store)`, `DisconnectDevTools()`, lazily-captured
  `BlexActionContext.GlobalState` (observers that never read the tree no longer pay for
  full-tree serialization).
- **DI**: stores registered via `AddBlexStore<T>()` attach to the manager on first resolution,
  so Blex works without `<BlexProvider>` (console apps, workers, tests).
- **Blazor**: `BlexComponentBase.OwnsSubscription(...)` ties any subscription token to the
  component's lifetime; `Dispose(bool)` is overridable for derived cleanup.
- **Generator diagnostics** BLEX008-BLEX016: static/readonly members, `Latest` without a
  CancellationToken, `async void` actions, discarded action return values, state-field/property
  name conflicts, by-ref parameters, generic methods, record stores, conflicting base types.
  Duplicate detection now also covers user-declared members, `StoreBase` members and
  effect-generated names.
- Sample app: Weather page (cancellable `Latest` effect with loading/error UI), Todos rebuilt on
  `EntityAdapter` + a real `[Effect]`, Counter page demonstrates `Batch`, `ResetState()` and
  labeled undo/redo.
- Repo: GitHub Actions CI, `.editorconfig`, `TreatWarningsAsErrors`, entity adapter benchmarks.

### Fixed (final review round)

- `RedactDevToolsKeys` now recurses into arrays, so secrets inside collection state (entity
  lists, array-typed action args) are redacted as documented.
- `BlexHistory`'s pipeline handlers are exception-isolated: a throwing `Changed` (UI) handler
  can no longer break dispatch for observers registered after history (e.g. lose a persistence
  save) — failures route to `OnError`.
- `EntityAdapter.UpdateOne` re-keying an entity onto an id that already exists merges cleanly
  instead of silently duplicating the id in `Ids`.
- Rehydration no longer deletes valid stored data when a `StateChanged` subscriber throws while
  the restored state is applied (only unreadable payloads are discarded).

### Fixed (verification review round)

- Generator: parameters or state fields named like C# keywords (`@lock`) or generated identifiers
  (`state`, `value`, `__ct`) produced uncompilable or silently wrong code; emitted code now
  `@`-escapes identifiers, `this.`-qualifies field accesses, and prefixes wrapper locals with
  `__blex`.
- Queue effects: a failed predecessor's error no longer survives a successful successor (the
  error is cleared only after the predecessor completes).
- A `StateChanged` subscriber that throws during a standalone set can no longer leak the internal
  dirty flag (which recorded a later no-op batch as a phantom action); the set is still recorded
  even when a subscriber throws, keeping persistence/history in sync with the applied mutation.
- `RestoreGlobalState` freezes in-flight action snapshots first, so a reactor that undoes an
  action (e.g. `history.Undo()` from a subscriber) cannot corrupt later observers' snapshots.
- `StatePersistor` isolates its own handler exceptions from the dispatch pipeline and snapshots
  the pending set before serializing (a store serializer that dispatches re-enters safely).
- README/doc comments no longer overstate cancellation semantics: only cancellation via the
  effect's own token is benign; foreign cancellations are recorded as errors.

### Fixed (adversarial review round)

- Generator: nullable-enum default parameters (`Mode? m = Mode.B`) emitted uncompilable code; a
  C# keyword as an explicit `[Action(Name = ...)]` produced an invalid method name (now treated
  as display label only); control characters in names broke both string literals and XML doc
  comments; a BLEX005 collision emitted the broken source alongside the diagnostic (now the
  diagnostic suppresses emission); collisions with generated backing fields (`__XValid`,
  `__XPending`, ...) are now detected; the transform output is fully equatable so incremental
  caching actually works.
- Effects: a superseded `Latest` run can no longer overwrite the newest run's error state; a
  foreign `OperationCanceledException` (e.g. an HttpClient timeout) is now recorded as an error
  instead of being mistaken for a benign cancel; `Queue` gates use
  `RunContinuationsAsynchronously` and can no longer be bricked by a throwing `StateChanged`
  subscriber (lifecycle setters moved inside the `try`); Queue self-invocation deadlock documented.
- `Unregister` now detaches the store, so its actions stop flowing to middleware/DevTools.
- Lazy `GlobalState` snapshots are frozen before a reactor's follow-up action mutates state, so
  every observer sees the state that belonged to *its* action; `BlexHistory` also refreshes its
  present snapshot after external restores (DevTools jumps), fixing stale undo targets.
- DevTools sanitizers now fail *closed* (state withheld + `OnError` report) instead of leaking
  unsanitized data when a sanitizer throws.
- `StatePersistor`: debounced saves stay on the captured synchronization context and all
  pending/write bookkeeping is lock-guarded (Blazor Server safety); optional `DebounceMaxDelay`
  bounds how long a steady action stream can postpone a save; the version envelope is only
  recognized in its exact shape (no collision with user state containing `__blexVersion`).
- `BlexProvider` no longer starts persistence twice concurrently when Blazor renders during
  initialization; the DevTools connector survives disposal races during fast navigation and
  broadened circuit-teardown exceptions.
- `RestoreGlobalState` applies outside the registry lock, so a `StateChanged` handler that
  registers/unregisters stores can no longer corrupt the restore.

### Fixed

- Restoring a snapshot now sets JSON-`null` properties back to `null`/default (previously a
  reference-typed field could never be restored to `null` by time-travel, undo or persistence).
- A synchronous `[Action]` invoked while an async action of the same store is awaiting no longer
  loses its change notification.
- DevTools receives an action *before* cross-store subscribers run, so reactor-dispatched
  follow-up actions can no longer invert the extension's timeline.
- Malformed DevTools messages and schema-drifted snapshots no longer throw across the JS interop
  boundary; per-store restore failures are isolated and reported.
- Overlapping runs of the same effect no longer corrupt the `IsLoading` flag.
- The generator preserves nullable reference annotations (`string?` state emitted correctly),
  disambiguates hint names by namespace, escapes string literals, fully qualifies JSON types, and
  propagates default parameter values and `params` modifiers to wrappers.
- The DevTools JS bridge unsubscribes its own connection instead of calling the extension-global
  `disconnect()`, and no longer advertises unimplemented features (pause/dispatch).
- `<BlexProvider>` no longer fails app startup under Blazor Server prerendering; browser-storage
  hydration is retried on first interactive render, and history starts recording only after
  hydration (undo baseline is the hydrated state).
- Fire-and-forget persistence writes could land out of order; writes are now chained.
- Sample Counter page no longer leaks a `BlexHistory.Changed` subscription per visit.
