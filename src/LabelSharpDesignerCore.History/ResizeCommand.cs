namespace LabelSharpDesignerCore.History;

public sealed record ResizeCommand : DocumentCommand
{
    public required string ElementId { get; init; }

    public override string Description => $"Redimensionar {ElementId}";
}
