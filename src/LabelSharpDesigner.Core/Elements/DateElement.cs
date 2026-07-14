using LabelSharpDesigner.Core.Styles;

namespace LabelSharpDesigner.Core.Elements;

public enum DateTimeValueSource
{
    Now,
    Variable,
}

public sealed record DateElement : LabelElement
{
    public required string Format { get; init; }
    public DateTimeValueSource Source { get; init; } = DateTimeValueSource.Now;
    public string? VariableName { get; init; }
    public TextStyleSpec Style { get; init; } = TextStyleSpec.Default;

    public override TResult Accept<TResult>(IElementVisitor<TResult> visitor) => visitor.VisitDate(this);
}
