using LabelSharpDesignerCore.Core.Styles;

namespace LabelSharpDesignerCore.Core.Elements;

public sealed record EllipseElement : LabelElement
{
    public ShapeStyleSpec Style { get; init; } = ShapeStyleSpec.Default;

    public override TResult Accept<TResult>(IElementVisitor<TResult> visitor) => visitor.VisitEllipse(this);
}
