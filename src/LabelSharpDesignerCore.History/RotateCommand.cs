namespace LabelSharpDesignerCore.History;

public sealed record RotateCommand : DocumentCommand
{
    public required string ElementId { get; init; }

    public override string Description => $"Rotacionar {ElementId}";
}
