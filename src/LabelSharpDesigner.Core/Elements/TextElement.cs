using LabelSharpDesigner.Core.Styles;

namespace LabelSharpDesigner.Core.Elements;

public sealed record TextElement : LabelElement
{
    public required string Content { get; init; }
    public string? StyleId { get; init; }
    public TextStyleSpec Style { get; init; } = TextStyleSpec.Default;

    public override TResult Accept<TResult>(IElementVisitor<TResult> visitor) => visitor.VisitText(this);
}
