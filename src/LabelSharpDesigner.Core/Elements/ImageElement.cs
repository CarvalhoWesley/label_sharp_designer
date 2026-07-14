using LabelSharpDesigner.Core.Geometry;

namespace LabelSharpDesigner.Core.Elements;

public enum ImageFit
{
    Contain,
    Cover,
    Fill,
    FitWidth,
    FitHeight,
    None,
}

public sealed record ImageElement : LabelElement
{
    public required string Source { get; init; }
    public ImageFit Fit { get; init; } = ImageFit.Contain;
    public PointMm? CropPosition { get; init; }
    public SizeMm? CropSize { get; init; }

    public override TResult Accept<TResult>(IElementVisitor<TResult> visitor) => visitor.VisitImage(this);
}
