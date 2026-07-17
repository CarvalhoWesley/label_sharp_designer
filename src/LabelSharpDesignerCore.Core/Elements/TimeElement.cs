using LabelSharpDesignerCore.Core.Styles;

namespace LabelSharpDesignerCore.Core.Elements;

public sealed record TimeElement : LabelElement
{
    public required string Format { get; init; }
    public DateTimeValueSource Source { get; init; } = DateTimeValueSource.Now;
    public string? VariableName { get; init; }
    public TextStyleSpec Style { get; init; } = TextStyleSpec.Default;

    public override TResult Accept<TResult>(IElementVisitor<TResult> visitor) => visitor.VisitTime(this);
}
