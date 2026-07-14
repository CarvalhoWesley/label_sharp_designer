namespace LabelSharpDesigner.History;

public sealed record ChangePropertyCommand : DocumentCommand
{
    public required IReadOnlyList<string> ElementIds { get; init; }

    public required string PropertyName { get; init; }

    public override string Description => ElementIds.Count == 1
        ? $"Alterar {PropertyName} de {ElementIds[0]}"
        : $"Alterar {PropertyName} de {ElementIds.Count} elementos";
}
