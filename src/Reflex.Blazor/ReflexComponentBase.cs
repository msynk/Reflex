using Microsoft.AspNetCore.Components;

namespace Reflex.Blazor;

/// <summary>
/// Optional base component that re-renders automatically when subscribed stores change.
/// Call <see cref="Subscribe(ReadOnlySpan{IStore})"/> in <c>OnInitialized</c>; unsubscription is automatic.
/// </summary>
public abstract class ReflexComponentBase : ComponentBase, IDisposable
{
    private readonly List<IStore> _subscriptions = [];
    private readonly List<IDisposable> _selectorSubscriptions = [];
    private bool _disposed;

    /// <summary>Subscribes to one or more stores; the component re-renders on any change.</summary>
    protected void Subscribe(params ReadOnlySpan<IStore> stores)
    {
        foreach (var store in stores)
        {
            if (store is null || _subscriptions.Contains(store))
                continue;
            store.StateChanged += OnStoreChanged;
            _subscriptions.Add(store);
        }
    }

    /// <summary>
    /// Subscribes to a projection of a store; the component re-renders only when the selected
    /// value changes, ignoring unrelated mutations to the same store. Useful for stores with many
    /// independent <c>[State]</c> fields or large collections.
    /// </summary>
    /// <example><code>Subscribe(Store, () => Store.Count);</code></example>
    protected void Subscribe<T>(IStore store, Func<T> selector, IEqualityComparer<T>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(selector);
        _selectorSubscriptions.Add(store.Subscribe(selector, (Action<T>)(_ => OnStoreChanged()), comparer));
    }

    /// <summary>
    /// Registers an arbitrary subscription token to be disposed with the component (e.g. a
    /// cross-store <c>manager.SubscribeTo&lt;T&gt;(...)</c> token or a selector subscription
    /// created manually).
    /// </summary>
    protected void OwnsSubscription(IDisposable subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        _selectorSubscriptions.Add(subscription);
    }

    private void OnStoreChanged()
    {
        if (_disposed)
            return;
        _ = InvokeAsync(StateHasChanged);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        foreach (var store in _subscriptions)
            store.StateChanged -= OnStoreChanged;
        _subscriptions.Clear();
        foreach (var sub in _selectorSubscriptions)
            sub.Dispose();
        _selectorSubscriptions.Clear();
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Override to release additional resources when the component is disposed (the store
    /// subscriptions are already detached when this runs).
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
    }
}
