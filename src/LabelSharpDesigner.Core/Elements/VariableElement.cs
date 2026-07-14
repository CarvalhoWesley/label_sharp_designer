using LabelSharpDesigner.Core.Styles;

namespace LabelSharpDesigner.Core.Elements;

public sealed record VariableElement : LabelElement
{
    public required string Expression { get; init; }
    public string? StyleId { get; init; }
    public TextStyleSpec Style { get; init; } = TextStyleSpec.Default;

    public override TResult Accept<TResult>(IElementVisitor<TResult> visitor) => visitor.VisitVariable(this);
}
