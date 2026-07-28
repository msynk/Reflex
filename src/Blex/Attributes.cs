namespace Blex;

/// <summary>
/// Marks a partial class as a Blex state store. The source generator will emit reactive
/// properties for <see cref="StateAttribute"/> fields, memoized <see cref="ComputedAttribute"/>
/// accessors, named action wrappers, JSON snapshot support and the <see cref="StoreBase"/> base type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class StoreAttribute : Attribute
{
    /// <summary>Optional display name used in DevTools. Defaults to the class name.</summary>
    public string? Name { get; set; }

    /// <summary>
    /// When <c>true</c>, the store opts in to automatic persistence: its state is saved through the
    /// registered <see cref="IBlexStorage"/> after every action and rehydrated on startup.
    /// Has no effect unless a persistence provider is wired up (see <c>AddBlexPersistence</c>).
    /// </summary>
    public bool Persist { get; set; }
}

/// <summary>
/// Marks a backing field as a piece of reactive state. The generator emits a public property
/// (PascalCased, with the leading underscore removed) whose setter flows through the dispatch
/// pipeline so changes are tracked, notified and recorded for time-travel.
/// </summary>
/// <remarks>
/// Change detection uses <see cref="EqualityComparer{T}.Default"/>. For reference types such as
/// <c>List&lt;T&gt;</c> that do not override equality, this is reference equality, so mutating a
/// collection in place will not raise a notification. Assign a new instance (or use an immutable
/// collection) when updating collection or object state.
/// </remarks>
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
    /// <summary>
    /// Optional explicit action name. When it is a valid C# identifier it is used as the public
    /// wrapper method name and the display label. When it contains characters that aren't valid in
    /// an identifier (such as spaces) it is treated purely as the display label, and the wrapper
    /// method name is derived from the implementation method by stripping its <c>On</c> prefix.
    /// </summary>
    public string? Name { get; set; }
}

/// <summary>
/// How overlapping invocations of the same <see cref="EffectAttribute">effect</see> are handled,
/// mirroring the RxJS flattening operators used by NgRx effects.
/// </summary>
public enum EffectConcurrency
{
    /// <summary>All invocations run concurrently (<c>mergeMap</c>). The default.</summary>
    Parallel = 0,

    /// <summary>
    /// A new invocation cancels the previous one (<c>switchMap</c> / "take latest"). Ideal for
    /// type-ahead search. Requires a <see cref="System.Threading.CancellationToken"/> parameter on
    /// the effect method for the running body to actually observe the cancellation. A superseded
    /// run can never overwrite the newest run's error state. An invocation vetoed by middleware
    /// does not supersede anything: the run already in flight keeps going.
    /// </summary>
    Latest = 1,

    /// <summary>
    /// New invocations are ignored while one is running (<c>exhaustMap</c> / "take leading").
    /// Ideal for guarding against double-clicked submit buttons. An invocation dropped this way
    /// never reaches the middleware pipeline -- nothing was dispatched.
    /// </summary>
    Drop = 2,

    /// <summary>
    /// Invocations run one at a time in arrival order (<c>concatMap</c>). Ideal for writes where
    /// ordering matters. A Queue effect must never invoke itself (directly or via a subscriber
    /// its body triggers) -- the nested call would wait on its own queue slot and deadlock.
    /// Middleware sees an invocation when it is made, not when it reaches the front of the queue,
    /// so a vetoed invocation never takes a slot and cannot delay the ones behind it.
    /// </summary>
    Queue = 3,
}

/// <summary>
/// Marks an asynchronous method (returning <c>Task</c> or <c>ValueTask</c>, named <c>OnXxx</c>) as an
/// effect: an async action whose loading/error lifecycle is managed for you. The generator emits the
/// public <c>Xxx(...)</c> wrapper plus reactive <c>XxxIsLoading</c> (bool) and <c>XxxError</c>
/// (<see cref="Exception"/>) properties. The wrapper keeps <c>IsLoading</c> <c>true</c> while any
/// run is in flight, captures any thrown exception into <c>Error</c> instead of propagating it
/// (cancellations are not treated as errors), and records the body as one action.
/// </summary>
/// <remarks>
/// When the effect method's last parameter is a <see cref="System.Threading.CancellationToken"/>,
/// the token is provided by the generated wrapper (it does not appear on the public method) and a
/// <c>CancelXxx()</c> method is emitted to cancel in-flight runs.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class EffectAttribute : Attribute
{
    /// <summary>
    /// Optional explicit name. A valid C# identifier becomes the wrapper method name and display
    /// label; a value with spaces is treated as the display label only (wrapper derived from <c>On</c>).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>How overlapping invocations of this effect are handled. Defaults to <see cref="EffectConcurrency.Parallel"/>.</summary>
    public EffectConcurrency Concurrency { get; set; }
}
