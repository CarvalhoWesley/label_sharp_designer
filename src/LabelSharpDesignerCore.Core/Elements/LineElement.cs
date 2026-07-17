using LabelSharpDesignerCore.Core.Styles;

namespace LabelSharpDesignerCore.Core.Elements;

public sealed record LineElement : LabelElement
{
    public ArgbColor StrokeColor { get; init; } = ArgbColor.Black;
    public double StrokeWidth { get; init; } = 0.3;

    public override TResult Accept<TResult>(IElementVisitor<TResult> visitor) => visitor.VisitLine(this);
}
