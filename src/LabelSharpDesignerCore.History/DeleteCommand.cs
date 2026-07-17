namespace LabelSharpDesignerCore.History;

public sealed record DeleteCommand : DocumentCommand
{
    public required IReadOnlyList<string> ElementIds { get; init; }

    public override string Description => ElementIds.Count == 1
        ? $"Excluir elemento {ElementIds[0]}"
        : $"Excluir {ElementIds.Count} elementos";
}
