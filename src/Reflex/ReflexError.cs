namespace Reflex;

/// <summary>
/// Describes a non-fatal failure that Reflex isolated from the dispatch pipeline (a throwing
/// subscriber, middleware, persistence write, restore, sanitizer, ...). Delivered to
/// <see cref="ReflexOptions.OnError"/> / <see cref="ReflexManager.OnError"/>; when no handler is
/// registered the error is written to <see cref="Console.Error"/>.
/// </summary>
/// <param name="Source">The pipeline area that failed (e.g. <c>"subscriber"</c>, <c>"middleware"</c>, <c>"persistence"</c>, <c>"restore"</c>, <c>"devtools"</c>).</param>
/// <param name="Exception">The captured exception.</param>
/// <param name="Detail">Optional context, such as the store or action involved.</param>
public readonly record struct ReflexError(string Source, Exception Exception, string? Detail = null);
