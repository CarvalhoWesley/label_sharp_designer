using LabelSharpDesignerCore.Core.Styles;

namespace LabelSharpDesignerCore.Core.Elements;

public sealed record RectangleElement : LabelElement
{
    public ShapeStyleSpec Style { get; init; } = ShapeStyleSpec.Default;
    public double CornerRadius { get; init; }

    public override TResult Accept<TResult>(IElementVisitor<TResult> visitor) => visitor.VisitRectangle(this);
}
