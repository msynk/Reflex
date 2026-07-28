namespace Blex;

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
public sealed class EffectAttributeBlex : Attribute
{
    /// <summary>
    /// Optional explicit name. A valid C# identifier becomes the wrapper method name and display
    /// label; a value with spaces is treated as the display label only (wrapper derived from <c>On</c>).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>How overlapping invocations of this effect are handled. Defaults to <see cref="EffectConcurrencyBlex.Parallel"/>.</summary>
    public EffectConcurrencyBlex Concurrency { get; set; }
}
