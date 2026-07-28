namespace Blex.Demo.Stores;

/// <summary>
/// Demonstrates the one rule that catches everybody: change detection uses
/// <see cref="EqualityComparer{T}.Default"/>, which for an ordinary collection means
/// <em>reference</em> equality. Mutating a list in place leaves the reference unchanged, so no
/// notification fires; assigning a new instance is what makes the change visible.
/// </summary>
[StoreAttributeBlex(Name = "equalityDemo")]
public partial class EqualityDemoStore
{
    private int _next = 1;

    [StateAttributeBlex] private List<string> _items = ["alpha"];

    [ComputedAttributeBlex] private int ComputeCount() => Items.Count;

    /// <summary>The trap: the list contents change but the reference does not, so nothing notifies.</summary>
    [ActionAttributeBlex]
    private void OnMutateInPlace() => Items.Add($"item {_next++}");

    /// <summary>The fix: assign a new list, so the reference differs and change detection fires.</summary>
    [ActionAttributeBlex]
    private void OnAssignNew() => Items = [.. Items, $"item {_next++}"];

    [ActionAttributeBlex]
    private void OnReset()
    {
        _next = 1;
        Items = ["alpha"];
    }
}
