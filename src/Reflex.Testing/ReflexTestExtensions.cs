using System.Text.Json.Nodes;

namespace Reflex.Testing;

/// <summary>Convenience extensions for testing Reflex stores and managers.</summary>
public static class ReflexTestExtensions
{
    /// <summary>Starts recording dispatched actions on the manager. Dispose the result to stop.</summary>
    public static ActionLog RecordActions(this ReflexManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);
        return new ActionLog(manager);
    }

    /// <summary>Returns the store's current state as a JSON snapshot (alias for SerializeState).</summary>
    public static JsonObject Snapshot(this IStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        return store.SerializeState();
    }

    /// <summary>
    /// Counts how many times the store notifies <see cref="IStore.StateChanged"/> while running
    /// <paramref name="act"/>. Useful for asserting batching (e.g. one notification per action).
    /// </summary>
    public static int CountNotifications(this IStore store, Action act)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(act);
        var count = 0;
        void Handler() => count++;
        store.StateChanged += Handler;
        try
        {
            act();
        }
        finally
        {
            store.StateChanged -= Handler;
        }

        return count;
    }
}
