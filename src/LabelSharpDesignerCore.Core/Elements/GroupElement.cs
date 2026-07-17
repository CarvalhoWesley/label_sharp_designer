namespace LabelSharpDesignerCore.Core.Elements;

public sealed record GroupElement : LabelElement
{
    public required IReadOnlyList<LabelElement> Children { get; init; }

    public override TResult Accept<TResult>(IElementVisitor<TResult> visitor) => visitor.VisitGroup(this);
}
