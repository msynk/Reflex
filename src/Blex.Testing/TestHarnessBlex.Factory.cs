using System.Text.Json.Nodes;

namespace Blex.Testing;

/// <summary>Factory helpers for <see cref="TestHarnessBlex{TStore}"/>.</summary>
public static class TestHarnessBlex
{
    /// <summary>Creates a harness for an already-constructed store.</summary>
    public static TestHarnessBlex<TStore> For<TStore>(TStore store) where TStore : StoreBaseBlex
        => new(store);

    /// <summary>Creates a harness for a store with a parameterless constructor.</summary>
    public static TestHarnessBlex<TStore> For<TStore>() where TStore : StoreBaseBlex, new()
        => new(new TStore());
}
