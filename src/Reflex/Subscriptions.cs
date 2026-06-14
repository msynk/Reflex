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
    public static IDisposable Subscribe<T>(
        this IStore store,
        Func<T> selector,
        Action<T> onChanged,
        IEqualityComparer<T>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(onChanged);
        return new SelectorSubscription<T>(store, selector, onChanged, comparer ?? EqualityComparer<T>.Default);
    }

    private sealed class SelectorSubscription<T> : IDisposable
    {
        private readonly IStore _store;
        private readonly Func<T> _selector;
        private readonly Action<T> _onChanged;
        private readonly IEqualityComparer<T> _comparer;
        private T _last;
        private bool _disposed;

        public SelectorSubscription(IStore store, Func<T> selector, Action<T> onChanged, IEqualityComparer<T> comparer)
        {
            _store = store;
            _selector = selector;
            _onChanged = onChanged;
            _comparer = comparer;
            _last = selector();
            store.StateChanged += OnStoreChanged;
        }

        private void OnStoreChanged()
        {
            var current = _selector();
            if (_comparer.Equals(current, _last))
                return;

            _last = current;
            _onChanged(current);
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
