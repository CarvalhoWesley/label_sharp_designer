using LabelSharpDesigner.Core.Geometry;

namespace LabelSharpDesigner.Core.Elements;

public abstract record LabelElement
{
    public required string Id { get; init; }
    public string? Name { get; init; }
    public required PointMm Position { get; init; }
    public required SizeMm Size { get; init; }
    public double RotationDegrees { get; init; }
    public bool Visible { get; init; } = true;
    public bool Locked { get; init; }
    public double Opacity { get; init; } = 1.0;
    public string? LayerId { get; init; }
    public int ZIndex { get; init; }
    public ElementTransform Transform { get; init; } = ElementTransform.Identity;

    public abstract TResult Accept<TResult>(IElementVisitor<TResult> visitor);
}
