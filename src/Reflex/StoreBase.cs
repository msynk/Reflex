using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

namespace Reflex;

/// <summary>
/// Base class for all Reflex stores. Provides change notification, batched dispatch and the
/// hooks the source generator wires up. You normally never derive from this directly --
/// decorate a partial class with <see cref="StoreAttribute"/> and the generator does it for you.
/// </summary>
public abstract class StoreBase : IStore
{
    private int _recordDepth;
    private int _notifyDepth;
    private bool _dirty;
    private ReflexStore? _manager;

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public event Action? StateChanged;

    /// <inheritdoc />
    public abstract JsonObject SerializeState();

    /// <inheritdoc />
    public abstract void DeserializeState(JsonObject state);

    /// <summary>True while the manager is applying a time-travel/restore snapshot.</summary>
    protected bool IsRestoring { get; private set; }

    /// <summary>Called by the generator to invalidate memoized computed values.</summary>
    protected virtual void InvalidateComputed()
    {
    }

    /// <summary>Called by the generator's deserialize implementation to set a backing field directly.</summary>
    protected void NotifyRestored()
    {
        InvalidateComputed();
        StateChanged?.Invoke();
    }

    internal void Attach(ReflexStore manager) => _manager = manager;

    /// <summary>
    /// Assigns a state backing field. Raises change notification immediately unless inside a
    /// synchronous <see cref="Dispatch(string, Action)"/> batch, and records a standalone
    /// "Set X" action when not inside any dispatch. Used by generated setters.
    /// </summary>
    protected void SetState<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        _dirty = true;

        // Inside a sync batch we defer the render until the action completes (single re-render).
        if (_notifyDepth == 0)
        {
            InvalidateComputed();
            StateChanged?.Invoke();
        }

        // Outside any dispatch this is a standalone action; record it now.
        if (_recordDepth == 0)
        {
            _dirty = false;
            _manager?.RecordAction(this, $"Set {propertyName}");
        }
    }

    /// <summary>
    /// Runs <paramref name="mutation"/> as a single named action: synchronous state changes inside
    /// it are batched into one notification and one time-travel entry.
    /// </summary>
    protected void Dispatch(string actionName, Action mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        _recordDepth++;
        _notifyDepth++;
        try
        {
            mutation();
        }
        finally
        {
            _notifyDepth--;
            _recordDepth--;
            if (_recordDepth == 0 && _dirty)
            {
                _dirty = false;
                InvalidateComputed();
                StateChanged?.Invoke();
                _manager?.RecordAction(this, actionName);
            }
        }
    }

    /// <summary>
    /// Async variant. State changes update the UI as they happen (e.g. a loading flag before an
    /// await), but the whole operation is recorded as one named action for time-travel.
    /// </summary>
    protected async Task DispatchAsync(string actionName, Func<Task> mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        _recordDepth++;
        try
        {
            await mutation().ConfigureAwait(true);
        }
        finally
        {
            _recordDepth--;
            if (_recordDepth == 0 && _dirty)
            {
                _dirty = false;
                _manager?.RecordAction(this, actionName);
            }
        }
    }
    /// <summary>
    /// Applies a restored snapshot without recording a new action. Invoked by the manager during
    /// time-travel. Raises <see cref="StateChanged"/> exactly once.
    /// </summary>
    internal void ApplyRestoredState(JsonObject state)
    {
        IsRestoring = true;
        try
        {
            DeserializeState(state);
        }
        finally
        {
            IsRestoring = false;
        }

        InvalidateComputed();
        StateChanged?.Invoke();
    }
}
