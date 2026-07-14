using LabelSharpDesigner.Core.Styles;

namespace LabelSharpDesigner.Core.Elements;

public sealed record CircleElement : LabelElement
{
    public ShapeStyleSpec Style { get; init; } = ShapeStyleSpec.Default;

    public override TResult Accept<TResult>(IElementVisitor<TResult> visitor) => visitor.VisitCircle(this);
}
