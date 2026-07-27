using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;

namespace Blex.Blazor;

/// <summary>
/// Bridges Blex stores to Blazor's <see cref="PersistentComponentState"/> so that state produced
/// during prerendering is handed to the interactive render without re-fetching, eliminating the
/// "double render" flicker. This is distinct from durable <see cref="IBlexStorage"/> persistence:
/// it only survives the prerender-to-interactive transition within a single page load.
/// </summary>
/// <remarks>
/// Applies to every registered store (the goal is render handoff, not selective durability). Stores
/// are restored eagerly in <see cref="TryRestore"/> and re-persisted via a registered callback.
/// </remarks>
public sealed class ComponentStatePersistence : IDisposable
{
    private const string KeyPrefix = "blex.cs:";

    private readonly PersistentComponentState _state;
    private readonly IReadOnlyList<IStore> _stores;
    private PersistingComponentStateSubscription _subscription;
    private bool _subscribed;

    /// <summary>Creates the bridge over the framework state service and the registered stores.</summary>
    public ComponentStatePersistence(PersistentComponentState state, IEnumerable<IStore> stores)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
        _stores = stores as IReadOnlyList<IStore> ?? stores.ToList();
    }

    /// <summary>
    /// Restores any state captured during prerendering into the matching stores and registers the
    /// callback that re-persists them. Call once during the provider's initialization.
    /// </summary>
    public void TryRestore()
    {
        foreach (var store in _stores)
        {
            if (store is not StoreBase sb)
                continue;

            if (_state.TryTakeFromJson<JsonObject>(KeyPrefix + store.Name, out var slice) && slice is not null)
                sb.ApplyRestoredState(slice);
        }

        if (!_subscribed)
        {
            _subscription = _state.RegisterOnPersisting(PersistAll);
            _subscribed = true;
        }
    }

    private Task PersistAll()
    {
        foreach (var store in _stores)
            _state.PersistAsJson(KeyPrefix + store.Name, store.SerializeState());
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_subscribed)
        {
            _subscription.Dispose();
            _subscribed = false;
        }
    }
}
