using System.Text.Json.Nodes;

namespace Blex;

/// <summary>
/// In-app undo/redo for the whole application state, independent of the Redux DevTools extension.
/// Snapshots the global state after each action and lets you step backwards/forwards. Restoring a
/// snapshot does not record a new action, so undo/redo never pollutes the history.
/// </summary>
/// <remarks>
/// Call <see cref="Start"/> once after all stores are registered to capture the baseline snapshot.
/// Single-threaded dispatch is assumed (Blazor's model).
/// </remarks>
public sealed class BlexHistory : IDisposable
{
    private readonly record struct Entry(JsonObject State, string? Label);

    private readonly BlexManager _manager;
    private readonly List<Entry> _undo = [];
    private readonly List<Entry> _redo = [];
    private JsonObject? _present;
    private string? _presentLabel;
    private bool _started;
    private bool _restoring;

    /// <summary>Creates a history bound to a manager. Set <paramref name="maxEntries"/> to cap memory.</summary>
    public BlexHistory(BlexManager manager, int maxEntries = 100)
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

    /// <summary>Number of states available to undo to.</summary>
    public int UndoCount => _undo.Count;

    /// <summary>Number of states available to redo to.</summary>
    public int RedoCount => _redo.Count;

    /// <summary>
    /// The qualified name of the action that <see cref="Undo"/> would revert (e.g. for an
    /// "Undo Increment" button label), or <c>null</c> when nothing can be undone.
    /// </summary>
    public string? NextUndoLabel => CanUndo ? _presentLabel : null;

    /// <summary>
    /// The qualified name of the action that <see cref="Redo"/> would re-apply, or <c>null</c>
    /// when nothing can be redone.
    /// </summary>
    public string? NextRedoLabel => _redo.Count > 0 ? _redo[^1].Label : null;

    /// <summary>Captures the current state as the baseline and begins recording. Idempotent.</summary>
    public void Start()
    {
        if (_started)
            return;
        _started = true;
        _present = _manager.CaptureGlobalState();
        _presentLabel = null;
        _manager.ActionDispatched += OnAction;
        _manager.StateRestored += OnExternalRestore;
    }

    private void OnExternalRestore()
    {
        if (_restoring)
            return;

        try
        {
            // An external restore (DevTools time-travel, manual RestoreGlobalState) changed the
            // state without an action. Refresh the present snapshot so the next Undo diffs from
            // what the user actually sees rather than a stale pre-jump state.
            _present = _manager.CaptureGlobalState();
            _presentLabel = null;
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            // This handler sits directly on the pipeline events; an escaping exception would
            // break dispatch for observers registered after it.
            _manager.ReportError("history", ex);
        }
    }

    private void OnAction(BlexActionContext context)
    {
        if (_restoring)
            return;

        try
        {
            if (_present is not null)
            {
                _undo.Add(new Entry(_present, _presentLabel));
                if (_undo.Count > MaxEntries)
                    _undo.RemoveAt(0);
            }

            _present = context.GlobalState;
            _presentLabel = context.QualifiedName;
            _redo.Clear();
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            // A throwing Changed handler (user UI code) must not break the dispatch pipeline
            // for observers registered after history (e.g. persistence).
            _manager.ReportError("history", ex, context.QualifiedName);
        }
    }

    /// <summary>Reverts to the previous state. No-op when <see cref="CanUndo"/> is false.</summary>
    public void Undo()
    {
        if (_undo.Count == 0)
            return;

        if (_present is not null)
            _redo.Add(new Entry(_present, _presentLabel));

        var entry = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _present = entry.State;
        _presentLabel = entry.Label;
        Restore(entry.State);
        Changed?.Invoke();
    }

    /// <summary>Re-applies the most recently undone state. No-op when <see cref="CanRedo"/> is false.</summary>
    public void Redo()
    {
        if (_redo.Count == 0)
            return;

        if (_present is not null)
            _undo.Add(new Entry(_present, _presentLabel));

        var entry = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _present = entry.State;
        _presentLabel = entry.Label;
        Restore(entry.State);
        Changed?.Invoke();
    }

    /// <summary>Clears all undo/redo history, keeping the current state as the new baseline.</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        _present = _manager.CaptureGlobalState();
        _presentLabel = null;
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
            _manager.StateRestored -= OnExternalRestore;
            _started = false;
        }
    }
}
