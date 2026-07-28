using System.Text.Json.Nodes;

namespace Blex;

/// <summary>Configuration for <see cref="StatePersistorBlex"/>.</summary>
public sealed class PersistenceOptionsBlex
{
    /// <summary>Prefix prepended to every store's storage key. Defaults to <c>"blex:"</c>.</summary>
    public string KeyPrefix { get; set; } = "blex:";

    /// <summary>
    /// When set, writes are coalesced: a burst of actions produces a single storage write after
    /// this quiet period instead of one write per action. Pending writes are flushed on dispose
    /// (and can be forced with <see cref="StatePersistorBlex.FlushAsync"/>). Defaults to <c>null</c>
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
