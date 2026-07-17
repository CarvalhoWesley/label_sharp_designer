namespace LabelSharpDesignerCore.Expressions.Tokenizing;

public sealed record Token(TokenType Type, string Text, int Position)
{
    public double NumberValue { get; init; }

    public string? StringValue { get; init; }
}
