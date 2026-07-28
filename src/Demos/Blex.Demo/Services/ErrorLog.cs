namespace Blex.Demo.Services;

/// <summary>
/// Sink for <see cref="OptionsBlex.OnError"/>. Blex isolates non-fatal failures from the
/// dispatch pipeline -- a throwing subscriber, middleware, persistence write, restore or
/// sanitizer -- and routes them here instead of tearing down the app. Without a handler they
/// would go to <see cref="Console.Error"/>.
/// </summary>
public sealed class ErrorLog
{
    private readonly List<ErrorBlex> _errors = [];

    /// <summary>Raised after an error is captured or the log is cleared.</summary>
    public event Action? Changed;

    /// <summary>The captured errors, newest first.</summary>
    public IReadOnlyList<ErrorBlex> Errors => _errors;

    /// <summary>Captures an isolated pipeline error. Wire this to <c>options.OnError</c>.</summary>
    public void Add(ErrorBlex error)
    {
        _errors.Insert(0, error);
        if (_errors.Count > 20)
            _errors.RemoveAt(_errors.Count - 1);
        Changed?.Invoke();
    }

    /// <summary>Empties the log.</summary>
    public void Clear()
    {
        _errors.Clear();
        Changed?.Invoke();
    }
}
