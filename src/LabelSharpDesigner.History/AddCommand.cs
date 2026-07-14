namespace LabelSharpDesigner.History;

public sealed record AddCommand : DocumentCommand
{
    public required IReadOnlyList<string> ElementIds { get; init; }

    public override string Description => ElementIds.Count == 1
        ? $"Adicionar elemento {ElementIds[0]}"
        : $"Adicionar {ElementIds.Count} elementos";
}
