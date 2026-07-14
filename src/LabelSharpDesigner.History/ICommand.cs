using LabelSharpDesigner.Core.Document;

namespace LabelSharpDesigner.History;

/// <summary>
/// A single undoable step. Documents are immutable, so a command is simply a labeled transition
/// between two whole-document snapshots rather than an inverse-operation pair.
/// </summary>
public interface ICommand
{
    string Description { get; }

    LabelDocument Before { get; }

    LabelDocument After { get; }
}
