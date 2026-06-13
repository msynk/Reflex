namespace Reflex;

/// <summary>
/// Marks a partial class as a Reflex state store. The source generator will emit reactive
/// properties for <see cref="StateAttribute"/> fields, memoized <see cref="ComputedAttribute"/>
/// accessors, named action wrappers, JSON snapshot support and the <see cref="StoreBase"/> base type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class StoreAttribute : Attribute
{
    /// <summary>Optional display name used in DevTools. Defaults to the class name.</summary>
    public string? Name { get; set; }
}

/// <summary>
/// Marks a backing field as a piece of reactive state. The generator emits a public property
/// (PascalCased, with the leading underscore removed) whose setter flows through the dispatch
/// pipeline so changes are tracked, notified and recorded for time-travel.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class StateAttribute : Attribute
{
}

/// <summary>
/// Marks a parameterless method (named <c>ComputeXxx</c> or <c>GetXxx</c>) as derived/computed state.
/// The generator emits a memoized public property <c>Xxx</c> that recomputes lazily after any state change.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ComputedAttribute : Attribute
{
}

/// <summary>
/// Marks a partial <c>void</c> method whose name starts with <c>On</c> as an action implementation.
/// The generator emits a public wrapper (with the <c>On</c> prefix stripped) that batches the
/// mutation into a single, named, time-travel-recorded action.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ActionAttribute : Attribute
{
    /// <summary>Optional explicit action name. Defaults to the wrapper method name.</summary>
    public string? Name { get; set; }
}
