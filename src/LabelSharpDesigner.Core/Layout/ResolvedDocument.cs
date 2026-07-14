namespace LabelSharpDesigner.Core.Layout;

public sealed record ResolvedDocument
{
    public required int WidthDots { get; init; }
    public required int HeightDots { get; init; }
    public required int Dpi { get; init; }
    public required IReadOnlyList<ResolvedElement> Elements { get; init; }
}
