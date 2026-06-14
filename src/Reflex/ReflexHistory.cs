using System.Text.Json.Nodes;

namespace Reflex;

/// <summary>
/// In-app undo/redo for the whole application state, independent of the Redux DevTools extension.
/// Snapshots the global state after each action and lets you step backwards/forwards. Restoring a
/// snapshot does not record a new action, so undo/redo never pollutes the history.
/// </summary>
/// <remarks>
/// Call <see cref="Start"/> once after all stores are registered to capture the baseline snapshot.
/// Single-threaded dispatch is assumed (Blazor's model).
/// </remarks>
public sealed class ReflexHistory : IDisposable
{
    private readonly ReflexManager _manager;
    private readonly List<JsonObject> _undo = [];
    private readonly List<JsonObject> _redo = [];
    private JsonObject? _present;
    private bool _started;
    private bool _restoring;

    /// <summary>Creates a history bound to a manager. Set <paramref name="maxEntries"/> to cap memory.</summary>
    public ReflexHistory(ReflexManager manager, int maxEntries = 100)
    {
        ArgumentNullException.ThrowIfNull(manager);
        if (maxEntries < 1)
            throw new ArgumentOutOfRangeException(nameof(maxEntries));
        _manager = manager;
        MaxEntries = maxEntries;
    }

    /// <summary>Maximum number of undo entries retained; older entries are discarded.</summary>
    public int MaxEntries { get; }

    /// <summary>Raised whenever undo/redo availability changes (handy for binding button state).</summary>
    public event Action? Changed;

    /// <summary>Whether there is a prior state to undo to.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>Whether there is a state to redo to.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Captures the current state as the baseline and begins recording. Idempotent.</summary>
    public void Start()
    {
        if (_started)
            return;
        _started = true;
        _present = _manager.CaptureGlobalState();
        _manager.ActionDispatched += OnAction;
    }

    private void OnAction(ReflexActionContext context)
    {
        if (_restoring)
            return;

        if (_present is not null)
        {
            _undo.Add(_present);
            if (_undo.Count > MaxEntries)
                _undo.RemoveAt(0);
        }

        _present = context.GlobalState;
        _redo.Clear();
        Changed?.Invoke();
    }

    /// <summary>Reverts to the previous state. No-op when <see cref="CanUndo"/> is false.</summary>
    public void Undo()
    {
        if (_undo.Count == 0)
            return;

        if (_present is not null)
            _redo.Add(_present);

        _present = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        Restore(_present);
        Changed?.Invoke();
    }

    /// <summary>Re-applies the most recently undone state. No-op when <see cref="CanRedo"/> is false.</summary>
    public void Redo()
    {
        if (_redo.Count == 0)
            return;

        if (_present is not null)
            _undo.Add(_present);

        _present = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        Restore(_present);
        Changed?.Invoke();
    }

    /// <summary>Clears all undo/redo history, keeping the current state as the new baseline.</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        _present = _manager.CaptureGlobalState();
        Changed?.Invoke();
    }

    private void Restore(JsonObject state)
    {
        _restoring = true;
        try
        {
            _manager.RestoreGlobalState(state);
        }
        finally
        {
            _restoring = false;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_started)
        {
            _manager.ActionDispatched -= OnAction;
            _started = false;
        }
    }
}
