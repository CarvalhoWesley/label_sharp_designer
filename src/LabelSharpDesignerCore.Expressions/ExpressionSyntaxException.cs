namespace LabelSharpDesignerCore.Expressions;

public sealed class ExpressionSyntaxException : Exception
{
    public int Position { get; }

    public ExpressionSyntaxException(string message, int position)
        : base($"{message} (position {position})")
    {
        Position = position;
    }
}
