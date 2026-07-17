namespace LabelSharpDesignerCore.History;

/// <summary>General-purpose structural change not covered by a more specific command (group,
/// ungroup, layer reorder, and similar whole-document edits).</summary>
public sealed record ChangeDocumentCommand : DocumentCommand
{
    public required string Reason { get; init; }

    public override string Description => Reason;
}
