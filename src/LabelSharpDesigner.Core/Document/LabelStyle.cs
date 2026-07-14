using LabelSharpDesigner.Core.Styles;

namespace LabelSharpDesigner.Core.Document;

public sealed record LabelStyle
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public TextStyleSpec? Text { get; init; }
    public ShapeStyleSpec? Shape { get; init; }
}
