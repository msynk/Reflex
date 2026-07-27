using System.Text.Json.Nodes;

namespace Reflex.Testing;

/// <summary>
/// A self-contained test fixture around a single store: it creates a <see cref="ReflexManager"/>,
/// registers the store, and records every action so tests can assert on dispatch behavior without
/// any Blazor or DI setup.
/// </summary>
/// <typeparam name="TStore">The store under test.</typeparam>
public sealed class ReflexTestHarness<TStore> : IDisposable
    where TStore : StoreBase
{
    /// <summary>Creates a harness, registering <paramref name="store"/> with a fresh manager.</summary>
    public ReflexTestHarness(TStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Store = store;
        Manager = new ReflexManager();
        Manager.Register(store);
        Log = Manager.RecordActions();
    }

    /// <summary>The store under test.</summary>
    public TStore Store { get; }

    /// <summary>The manager backing the harness.</summary>
    public ReflexManager Manager { get; }

    /// <summary>The action log recording all dispatches.</summary>
    public ActionLog Log { get; }

    /// <summary>The current global state tree.</summary>
    public JsonObject State => Manager.CaptureGlobalState();

    /// <summary>The current snapshot of the store under test.</summary>
    public JsonObject Snapshot() => Store.SerializeState();

    /// <inheritdoc />
    public void Dispose() => Log.Dispose();
}

/// <summary>Factory helpers for <see cref="ReflexTestHarness{TStore}"/>.</summary>
public static class ReflexTestHarness
{
    /// <summary>Creates a harness for an already-constructed store.</summary>
    public static ReflexTestHarness<TStore> For<TStore>(TStore store) where TStore : StoreBase
        => new(store);

    /// <summary>Creates a harness for a store with a parameterless constructor.</summary>
    public static ReflexTestHarness<TStore> For<TStore>() where TStore : StoreBase, new()
        => new(new TStore());
}
