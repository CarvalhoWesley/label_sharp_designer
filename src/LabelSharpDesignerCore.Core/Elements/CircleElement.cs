using LabelSharpDesignerCore.Core.Styles;

namespace LabelSharpDesignerCore.Core.Elements;

public sealed record CircleElement : LabelElement
{
    public ShapeStyleSpec Style { get; init; } = ShapeStyleSpec.Default;

    public override TResult Accept<TResult>(IElementVisitor<TResult> visitor) => visitor.VisitCircle(this);
}
