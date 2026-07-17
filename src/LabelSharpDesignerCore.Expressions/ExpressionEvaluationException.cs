namespace LabelSharpDesignerCore.Expressions;

public sealed class ExpressionEvaluationException : Exception
{
    public ExpressionEvaluationException(string message)
        : base(message)
    {
    }
}
