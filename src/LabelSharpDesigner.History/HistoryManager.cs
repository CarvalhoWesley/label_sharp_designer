using LabelSharpDesigner.Core.Document;

namespace LabelSharpDesigner.History;

/// <summary>
/// Owns the undo/redo stacks for a single document editing session. <see cref="PreviewChange"/>
/// updates <see cref="Current"/> without touching the stacks (for live feedback mid-gesture, e.g.
/// while dragging); <see cref="Execute"/> commits a finished gesture as one undoable step.
/// </summary>
public sealed class HistoryManager
{
    private readonly Stack<ICommand> _undoStack = new();
    private readonly Stack<ICommand> _redoStack = new();

    public HistoryManager(LabelDocument initial)
    {
        Current = initial;
    }

    /// <summary><see cref="HistoryChangedEventArgs.IsPreview"/> is true for <see cref="PreviewChange"/>
    /// (fired on every mouse-move of a drag/resize/rotate gesture) and false for a committed change
    /// (<see cref="Execute"/>/<see cref="Undo"/>/<see cref="Redo"/>). Expensive listeners (panel
    /// rebuilds, re-layout) should skip preview notifications and only react once a gesture commits.</summary>
    public event EventHandler<HistoryChangedEventArgs>? Changed;

    public LabelDocument Current { get; private set; }

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    public string? UndoDescription => _undoStack.Count > 0 ? _undoStack.Peek().Description : null;

    public string? RedoDescription => _redoStack.Count > 0 ? _redoStack.Peek().Description : null;

    public void PreviewChange(LabelDocument document)
    {
        Current = document;
        Changed?.Invoke(this, HistoryChangedEventArgs.Preview);
    }

    public void Execute(ICommand command)
    {
        Current = command.After;
        _undoStack.Push(command);
        _redoStack.Clear();
        Changed?.Invoke(this, HistoryChangedEventArgs.Committed);
    }

    public void Undo()
    {
        if (!CanUndo)
        {
            return;
        }

        var command = _undoStack.Pop();
        Current = command.Before;
        _redoStack.Push(command);
        Changed?.Invoke(this, HistoryChangedEventArgs.Committed);
    }

    public void Redo()
    {
        if (!CanRedo)
        {
            return;
        }

        var command = _redoStack.Pop();
        Current = command.After;
        _undoStack.Push(command);
        Changed?.Invoke(this, HistoryChangedEventArgs.Committed);
    }
}

public sealed class HistoryChangedEventArgs : EventArgs
{
    public static readonly HistoryChangedEventArgs Preview = new() { IsPreview = true };
    public static readonly HistoryChangedEventArgs Committed = new() { IsPreview = false };

    public required bool IsPreview { get; init; }
}
