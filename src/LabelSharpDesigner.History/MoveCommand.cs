namespace LabelSharpDesigner.History;

public sealed record MoveCommand : DocumentCommand
{
    public required IReadOnlyList<string> ElementIds { get; init; }

    public override string Description => ElementIds.Count == 1
        ? $"Mover elemento {ElementIds[0]}"
        : $"Mover {ElementIds.Count} elementos";
}
