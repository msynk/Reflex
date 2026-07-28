using System.Text.Json.Nodes;

namespace Blex;

/// <summary>
/// Storage-agnostic, asynchronous key/value sink used to persist store state. Implementations
/// might wrap browser <c>localStorage</c>/<c>sessionStorage</c>, a file, a database, etc. The
/// core library ships only this abstraction; concrete browser storage lives in <c>Blex.Blazor</c>.
/// </summary>
public interface IBlexStorage
{
    /// <summary>Reads the raw string stored under <paramref name="key"/>, or <c>null</c> if absent.</summary>
    ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Writes <paramref name="value"/> under <paramref name="key"/>.</summary>
    ValueTask SetAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>Removes any value stored under <paramref name="key"/>.</summary>
    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>Configuration for <see cref="StatePersistor"/>.</summary>
public sealed class BlexPersistenceOptions
{
    /// <summary>Prefix prepended to every store's storage key. Defaults to <c>"blex:"</c>.</summary>
    public string KeyPrefix { get; set; } = "blex:";

    /// <summary>
    /// When set, writes are coalesced: a burst of actions produces a single storage write after
    /// this quiet period instead of one write per action. Pending writes are flushed on dispose
    /// (and can be forced with <see cref="StatePersistor.FlushAsync"/>). Defaults to <c>null</c>
    /// (write immediately after every action).
    /// </summary>
    public TimeSpan? DebounceInterval { get; set; }

    /// <summary>
    /// Upper bound on how long a debounced write may be postponed. Debouncing is trailing-edge,
    /// so a steady stream of actions would otherwise defer saving indefinitely; when the oldest
    /// pending change exceeds this age, a save is forced. Defaults to <c>null</c> (no cap).
    /// Only meaningful together with <see cref="DebounceInterval"/>.
    /// </summary>
    public TimeSpan? DebounceMaxDelay { get; set; }

    /// <summary>
    /// Schema version of the persisted payload. When greater than zero, saved state is wrapped in
    /// a version envelope; on load, a stored version that differs from this value is passed to
    /// <see cref="Migrate"/> (or discarded when no migration is registered). Bump this whenever a
    /// persisted store's shape changes. Defaults to <c>0</c> (no envelope, no version checking).
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Migration hook invoked when a persisted payload's version differs from <see cref="Version"/>.
    /// Receives the store name, the stored version (<c>0</c> for pre-versioning payloads) and the
    /// stored state; returns the migrated state, or <c>null</c> to discard the payload.
    /// </summary>
    public Func<string, int, JsonObject, JsonObject?>? Migrate { get; set; }
}

/// <summary>
/// Coordinates automatic persistence: rehydrates persistent stores on startup and saves their
/// slice through an <see cref="IBlexStorage"/> after every action (optionally debounced).
/// A store opts in via <c>[Store(Persist = true)]</c>. Restored state (undo/redo, time-travel)
/// is also written back so a reload never resurrects pre-restore state. Writes are serialized
/// in dispatch order so a stale payload can never overwrite a newer one.
/// Single-threaded dispatch is assumed (Blazor's model).
/// </summary>
public sealed class StatePersistor : IAsyncDisposable, IDisposable
{
    private const string VersionKey = "__blexVersion";
    private const string StateKey = "state";

    private readonly BlexManager _manager;
    private readonly IBlexStorage _storage;
    private readonly BlexPersistenceOptions _options;
    private readonly HashSet<string> _pending = [];

    // Debounced saves can resume off the dispatch thread when no SynchronizationContext exists
    // (plain hosts); this gate keeps the pending set, the debounce source and the write chain
    // consistent in that case.
    private readonly Lock _saveGate = new();
    private Task _writeChain = Task.CompletedTask;
    private CancellationTokenSource? _debounce;
    private DateTimeOffset? _pendingSince;
    private bool _subscribed;
    private bool _suspended;

    /// <summary>Creates a persistor over a manager and storage sink.</summary>
    public StatePersistor(BlexManager manager, IBlexStorage storage, BlexPersistenceOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(storage);
        _manager = manager;
        _storage = storage;
        _options = options ?? new BlexPersistenceOptions();
    }

    private string KeyFor(IStore store) => _options.KeyPrefix + store.Name;

    /// <summary>
    /// Loads and applies any previously persisted state for every persistent store, then starts
    /// listening for changes. Restoration does not record new actions. A corrupt or
    /// non-migratable payload is reported, discarded and skipped -- it never prevents startup.
    /// Exceptions from the storage provider itself (e.g. JS interop unavailable during
    /// prerendering) propagate so the host can retry later; the method is safe to call again.
    /// </summary>
    /// <remarks>
    /// Only stores already registered with the manager are rehydrated. In hosts without
    /// <c>&lt;BlexProvider&gt;</c>, resolve every store from DI (which registers it) before
    /// calling this method.
    /// </remarks>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        // Suspend saving while we restore so rehydration doesn't immediately rewrite storage.
        _suspended = true;
        try
        {
            foreach (var store in _manager.Stores)
            {
                if (!store.Persist || store is not StoreBase sb)
                    continue;

                // Storage access errors propagate (the storage may simply not be ready yet);
                // payload errors below are contained per store.
                var raw = await _storage.GetAsync(KeyFor(store), cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrEmpty(raw))
                    continue;

                JsonObject? slice = null;
                try
                {
                    slice = Unwrap(store.Name, raw);
                }
                catch (Exception ex)
                {
                    // Unreadable payload: report, discard, and drop the stored entry.
                    _manager.ReportError("persistence", ex, $"rehydrating '{store.Name}'");
                    await TryRemoveAsync(store, cancellationToken).ConfigureAwait(false);
                }

                if (slice is null)
                    continue;

                try
                {
                    sb.ApplyRestoredState(slice);
                }
                catch (Exception ex)
                {
                    // Applying can throw from a StateChanged subscriber even though the payload
                    // itself is fine -- report, but keep the stored data.
                    _manager.ReportError("persistence", ex, $"applying '{store.Name}'");
                }
            }
        }
        finally
        {
            _suspended = false;
        }

        if (!_subscribed)
        {
            _manager.ActionDispatched += OnAction;
            _manager.StateRestored += OnStateRestored;
            _subscribed = true;
        }
    }

    /// <summary>
    /// Parses a stored payload, unwrapping the version envelope and running migrations.
    /// Returns <c>null</c> when the payload should be discarded.
    /// </summary>
    private JsonObject? Unwrap(string storeName, string raw)
    {
        if (JsonNode.Parse(raw) is not JsonObject parsed)
            return null;

        var storedVersion = 0;
        var state = parsed;

        // An envelope has exactly { "__blexVersion": n, "state": {...} }; requiring the exact
        // shape prevents misreading a raw store payload that happens to contain a key named
        // "__blexVersion".
        if (parsed.Count == 2
            && parsed.TryGetPropertyValue(VersionKey, out var versionNode)
            && parsed.TryGetPropertyValue(StateKey, out var stateNode))
        {
            storedVersion = versionNode?.GetValue<int>() ?? 0;
            state = stateNode as JsonObject ?? new JsonObject();
            state = (JsonObject)state.DeepClone(); // detach from the envelope parent
        }

        if (storedVersion == _options.Version)
            return state;

        return _options.Migrate?.Invoke(storeName, storedVersion, state);
    }

    private string Wrap(JsonObject state)
    {
        if (_options.Version <= 0)
            return state.ToJsonString();

        var envelope = new JsonObject
        {
            [VersionKey] = _options.Version,
            [StateKey] = state,
        };
        return envelope.ToJsonString();
    }

    private async ValueTask TryRemoveAsync(IStore store, CancellationToken cancellationToken)
    {
        try
        {
            await _storage.RemoveAsync(KeyFor(store), cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Removal of a corrupt entry is best-effort.
        }
    }

    private void OnAction(BlexActionContext context)
    {
        if (_suspended || !context.Store.Persist)
            return;

        try
        {
            Schedule(context.Store.Name);
        }
        catch (Exception ex)
        {
            // This handler sits directly on ActionDispatched; an escaping exception would
            // break the dispatch pipeline for every other observer.
            _manager.ReportError("persistence", ex, $"scheduling '{context.Store.Name}'");
        }
    }

    private void OnStateRestored()
    {
        if (_suspended)
            return;

        try
        {
            // A restore (undo/redo, time-travel) changed every store at once; write back all
            // persistent slices so a reload matches what the user sees.
            foreach (var store in _manager.Stores)
            {
                if (store.Persist)
                    Schedule(store.Name);
            }
        }
        catch (Exception ex)
        {
            _manager.ReportError("persistence", ex, "scheduling restored state");
        }
    }

    private void Schedule(string storeName)
    {
        lock (_saveGate)
        {
            _pending.Add(storeName);

            if (_options.DebounceInterval is not { } interval)
            {
                SavePendingLocked();
                return;
            }

            // Trailing-edge debounce would postpone forever under a steady action stream;
            // enforce the optional max-latency cap.
            var now = DateTimeOffset.UtcNow;
            _pendingSince ??= now;
            if (_options.DebounceMaxDelay is { } maxDelay && now - _pendingSince >= maxDelay)
            {
                CancelDebounceLocked();
                SavePendingLocked();
                return;
            }

            CancelDebounceLocked();
            _debounce = new CancellationTokenSource();
            _ = DebouncedSaveAsync(interval, _debounce.Token);
        }
    }

    private void CancelDebounceLocked()
    {
        if (_debounce is { } previous)
        {
            previous.Cancel();
            previous.Dispose();
            _debounce = null;
        }
    }

    private async Task DebouncedSaveAsync(TimeSpan interval, CancellationToken token)
    {
        try
        {
            // No ConfigureAwait(false): stay on the captured context (the Blazor dispatcher)
            // when one exists, so store serialization runs on the dispatch thread.
            await Task.Delay(interval, token);
        }
        catch (OperationCanceledException)
        {
            return; // superseded by a newer action or disposed
        }

        lock (_saveGate)
        {
            SavePendingLocked();
        }
    }

    /// <summary>Serializes each pending store now and chains the writes in order. Caller holds <see cref="_saveGate"/>.</summary>
    private void SavePendingLocked()
    {
        _pendingSince = null;
        if (_pending.Count == 0)
            return;

        // Snapshot first: SerializeState is user code and may dispatch, which re-enters
        // Schedule (the lock is reentrant) and would otherwise mutate the set mid-enumeration.
        var pending = new string[_pending.Count];
        _pending.CopyTo(pending);
        _pending.Clear();

        foreach (var name in pending)
        {
            var store = _manager.GetStore(name);
            if (store is null)
                continue;

            string json;
            try
            {
                json = Wrap(store.SerializeState());
            }
            catch (Exception ex)
            {
                _manager.ReportError("persistence", ex, $"serializing '{name}'");
                continue;
            }

            var key = KeyFor(store);
            _writeChain = WriteAsync(_writeChain, key, json);
        }
    }

    private async Task WriteAsync(Task previous, string key, string json)
    {
        await previous.ConfigureAwait(false);
        try
        {
            await _storage.SetAsync(key, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _manager.ReportError("persistence", ex, $"writing '{key}'");
        }
    }

    /// <summary>
    /// Forces any debounced pending state to be written now and waits for all queued writes to
    /// complete. Useful before navigating away or in tests.
    /// </summary>
    public Task FlushAsync()
    {
        lock (_saveGate)
        {
            CancelDebounceLocked();
            SavePendingLocked();
            return _writeChain;
        }
    }

    /// <summary>Removes every persistent store's saved slice from storage.</summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        Task chain;
        lock (_saveGate)
        {
            CancelDebounceLocked();
            _pending.Clear();
            _pendingSince = null;
            chain = _writeChain;
        }

        await chain.ConfigureAwait(false);
        foreach (var store in _manager.Stores)
        {
            if (store.Persist)
                await _storage.RemoveAsync(KeyFor(store), cancellationToken).ConfigureAwait(false);
        }
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
            return;

        _manager.ActionDispatched -= OnAction;
        _manager.StateRestored -= OnStateRestored;
        _subscribed = false;
    }

    /// <summary>Stops listening, flushes any pending writes and waits for them to complete.</summary>
    public async ValueTask DisposeAsync()
    {
        Unsubscribe();
        await FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Synchronous disposal, for DI containers that are torn down with <see cref="IDisposable"/>.
    /// </summary>
    /// <remarks>
    /// A container registered as a singleton is disposed through whichever interface the host
    /// calls, and <c>ServiceProvider.Dispose()</c> throws outright on a service that only offers
    /// <see cref="IAsyncDisposable"/> -- so both have to exist. This one stops listening and hands
    /// any debounced state to the storage, but does not wait for the writes to finish: blocking a
    /// UI thread on storage during shutdown risks a deadlock. A synchronous
    /// <see cref="IBlexStorage"/> (MAUI <c>Preferences</c>, a file) completes inline either way;
    /// prefer <see cref="DisposeAsync"/> when the storage is genuinely asynchronous.
    /// </remarks>
    public void Dispose()
    {
        Unsubscribe();
        lock (_saveGate)
        {
            CancelDebounceLocked();
            SavePendingLocked();
        }
    }
}
