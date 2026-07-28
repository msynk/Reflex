namespace Blex;

/// <summary>
/// Marks a partial class as a Blex state store. The source generator will emit reactive
/// properties for <see cref="StateAttributeBlex"/> fields, memoized <see cref="ComputedAttributeBlex"/>
/// accessors, named action wrappers, JSON snapshot support and the <see cref="StoreBaseBlex"/> base type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class StoreAttributeBlex : Attribute
{
    /// <summary>Optional display name used in DevTools. Defaults to the class name.</summary>
    public string? Name { get; set; }

    /// <summary>
    /// When <c>true</c>, the store opts in to automatic persistence: its state is saved through the
    /// registered <see cref="IStorageBlex"/> after every action and rehydrated on startup.
    /// Has no effect unless a persistence provider is wired up (see <c>AddBlexPersistence</c>).
    /// </summary>
    public bool Persist { get; set; }
}
