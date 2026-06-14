using System.Text.Json.Nodes;

namespace Reflex;

/// <summary>
/// Storage-agnostic, asynchronous key/value sink used to persist store state. Implementations
/// might wrap browser <c>localStorage</c>/<c>sessionStorage</c>, a file, a database, etc. The
/// core library ships only this abstraction; concrete browser storage lives in <c>Reflex.Blazor</c>.
/// </summary>
public interface IReflexStorage
{
    /// <summary>Reads the raw string stored under <paramref name="key"/>, or <c>null</c> if absent.</summary>
    ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Writes <paramref name="value"/> under <paramref name="key"/>.</summary>
    ValueTask SetAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>Removes any value stored under <paramref name="key"/>.</summary>
    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>Configuration for <see cref="StatePersistor"/>.</summary>
public sealed class ReflexPersistenceOptions
{
    /// <summary>Prefix prepended to every store's storage key. Defaults to <c>"reflex:"</c>.</summary>
    public string KeyPrefix { get; set; } = "reflex:";
}

/// <summary>
/// Coordinates automatic persistence: rehydrates persistent stores on startup and saves their
/// slice through an <see cref="IReflexStorage"/> after every action. A store opts in via
/// <c>[Store(Persist = true)]</c>. Single-threaded dispatch is assumed (Blazor's model).
/// </summary>
public sealed class StatePersistor : IAsyncDisposable
{
    private readonly ReflexManager _manager;
    private readonly IReflexStorage _storage;
    private readonly string _prefix;
    private bool _subscribed;
    private bool _suspended;

    /// <summary>Creates a persistor over a manager and storage sink.</summary>
    public StatePersistor(ReflexManager manager, IReflexStorage storage, ReflexPersistenceOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(storage);
        _manager = manager;
        _storage = storage;
        _prefix = (options ?? new ReflexPersistenceOptions()).KeyPrefix;
    }

    private string KeyFor(IStore store) => _prefix + store.Name;

    /// <summary>
    /// Loads and applies any previously persisted state for every persistent store, then starts
    /// listening for changes. Restoration does not record new actions. Safe to call once.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        // Suspend saving while we restore so rehydration doesn't immediately rewrite storage.
        _suspended = true;
        try
        {
            foreach (var store in _manager.Stores)
            {
                if (!store.Persist)
                    continue;

                var raw = await _storage.GetAsync(KeyFor(store), cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrEmpty(raw))
                    continue;

                if (JsonNode.Parse(raw) is JsonObject slice && store is StoreBase sb)
                    sb.ApplyRestoredState(slice);
            }
        }
        finally
        {
            _suspended = false;
        }

        if (!_subscribed)
        {
            _manager.ActionDispatched += OnAction;
            _subscribed = true;
        }
    }

    private void OnAction(ReflexActionContext context)
    {
        if (_suspended || !context.Store.Persist)
            return;

        var key = KeyFor(context.Store);
        var json = context.Store.SerializeState().ToJsonString();
        FireAndForget(_storage.SetAsync(key, json));
    }

    /// <summary>Removes every persistent store's saved slice from storage.</summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        foreach (var store in _manager.Stores)
        {
            if (store.Persist)
                await _storage.RemoveAsync(KeyFor(store), cancellationToken).ConfigureAwait(false);
        }
    }

    private static void FireAndForget(ValueTask task)
    {
        if (task.IsCompletedSuccessfully)
            return;

        _ = Awaited(task);

        static async Task Awaited(ValueTask t)
        {
            try
            {
                await t.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Reflex] persistence write failed: {ex.Message}");
            }
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_subscribed)
        {
            _manager.ActionDispatched -= OnAction;
            _subscribed = false;
        }

        return ValueTask.CompletedTask;
    }
}
