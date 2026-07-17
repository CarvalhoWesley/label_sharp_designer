namespace LabelSharpDesignerCore.Core.Document;

public sealed record LabelLayer
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public bool Visible { get; init; } = true;
    public bool Locked { get; init; }
    public int Order { get; init; }
}
