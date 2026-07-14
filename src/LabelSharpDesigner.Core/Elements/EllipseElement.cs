using LabelSharpDesigner.Core.Styles;

namespace LabelSharpDesigner.Core.Elements;

public sealed record EllipseElement : LabelElement
{
    public ShapeStyleSpec Style { get; init; } = ShapeStyleSpec.Default;

    public override TResult Accept<TResult>(IElementVisitor<TResult> visitor) => visitor.VisitEllipse(this);
}
