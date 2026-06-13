using Microsoft.AspNetCore.Components;

namespace Reflex.Blazor;

/// <summary>
/// Optional base component that re-renders automatically when subscribed stores change.
/// Call <see cref="Subscribe(IStore[])"/> in <c>OnInitialized</c>; unsubscription is automatic.
/// </summary>
public abstract class ReflexComponentBase : ComponentBase, IDisposable
{
    private readonly List<IStore> _subscriptions = new();
    private bool _disposed;

    /// <summary>Subscribes to one or more stores; the component re-renders on any change.</summary>
    protected void Subscribe(params IStore[] stores)
    {
        foreach (var store in stores)
        {
            if (store is null || _subscriptions.Contains(store))
                continue;
            store.StateChanged += OnStoreChanged;
            _subscriptions.Add(store);
        }
    }

    private void OnStoreChanged() => InvokeAsync(StateHasChanged);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        foreach (var store in _subscriptions)
            store.StateChanged -= OnStoreChanged;
        _subscriptions.Clear();
        GC.SuppressFinalize(this);
    }
}
