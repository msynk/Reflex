using System.Text.Json.Nodes;

namespace Reflex;

/// <summary>
/// A reactive state container. Implemented by <see cref="StoreBase"/> and the generated stores.
/// </summary>
public interface IStore
{
    /// <summary>Display name of the store (used as the slice key in DevTools state).</summary>
    string Name { get; }

    /// <summary>Raised after the store's state changes so views can re-render.</summary>
    event Action? StateChanged;

    /// <summary>Captures the current state as a JSON object for snapshots / DevTools.</summary>
    JsonObject SerializeState();

    /// <summary>
    /// Restores state from a JSON object produced by <see cref="SerializeState"/>.
    /// Used for time-travel and persistence. Does not record a new action.
    /// </summary>
    void DeserializeState(JsonObject state);
}
