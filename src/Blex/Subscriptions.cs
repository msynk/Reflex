namespace Reflex;

/// <summary>
/// Selector-based subscription helpers. Where <see cref="IStore.StateChanged"/> fires on every
/// change, these fire a callback only when a projected value actually changes, so consumers
/// (e.g. components) can avoid re-rendering for unrelated state mutations.
/// </summary>
public static class StoreSubscriptionExtensions
{
    /// <summary>
    /// Observes a projection of the store and invokes <paramref name="onChanged"/> only when the
    /// selected value differs from the previous one (per <paramref name="comparer"/> or the default
    /// equality comparer). Dispose the returned token to stop observing.
    /// </summary>
    /// <param name="store">The store to observe.</param>
    /// <param name="selector">Projects the observed value from the store.</param>
    /// <param name="onChanged">Invoked with the new value whenever it changes.</param>
    /// <param name="comparer">Custom equality; defaults to <see cref="EqualityComparer{T}.Default"/>.</param>
    /// <param name="fireImmediately">When <c>true</c>, invokes the callback once with the current value on subscription (like MobX's <c>fireImmediately</c>).</param>
    public static IDisposable Subscribe<T>(
        this IStore store,
        Func<T> selector,
        Action<T> onChanged,
        IEqualityComparer<T>? comparer = null,
        bool fireImmediately = false)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(onChanged);
        return new SelectorSubscription<T>(store, selector, (_, current) => onChanged(current), comparer ?? EqualityComparer<T>.Default, fireImmediately);
    }

    /// <summary>
    /// Observes a projection of the store and invokes <paramref name="onChanged"/> with the
    /// previous and new values whenever the selection changes -- handy for transition logic
    /// (analytics, animations, "changed from X to Y" logging).
    /// </summary>
    /// <param name="store">The store to observe.</param>
    /// <param name="selector">Projects the observed value from the store.</param>
    /// <param name="onChanged">Invoked with <c>(previous, current)</c> whenever the value changes.</param>
    /// <param name="comparer">Custom equality; defaults to <see cref="EqualityComparer{T}.Default"/>.</param>
    /// <param name="fireImmediately">When <c>true</c>, invokes the callback once on subscription; previous and current are both the value at subscription time.</param>
    public static IDisposable Subscribe<T>(
        this IStore store,
        Func<T> selector,
        Action<T, T> onChanged,
        IEqualityComparer<T>? comparer = null,
        bool fireImmediately = false)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(onChanged);
        return new SelectorSubscription<T>(store, selector, onChanged, comparer ?? EqualityComparer<T>.Default, fireImmediately);
    }

    private sealed class SelectorSubscription<T> : IDisposable
    {
        private readonly IStore _store;
        private readonly Func<T> _selector;
        private readonly Action<T, T> _onChanged;
        private readonly IEqualityComparer<T> _comparer;
        private T _last;
        private bool _disposed;

        public SelectorSubscription(IStore store, Func<T> selector, Action<T, T> onChanged, IEqualityComparer<T> comparer, bool fireImmediately)
        {
            _store = store;
            _selector = selector;
            _onChanged = onChanged;
            _comparer = comparer;
            _last = selector();
            store.StateChanged += OnStoreChanged;
            if (fireImmediately)
                onChanged(_last, _last);
        }

        private void OnStoreChanged()
        {
            var current = _selector();
            if (_comparer.Equals(current, _last))
                return;

            var previous = _last;
            _last = current;
            _onChanged(previous, current);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _store.StateChanged -= OnStoreChanged;
        }
    }
}
