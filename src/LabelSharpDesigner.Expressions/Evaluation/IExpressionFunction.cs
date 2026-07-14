namespace LabelSharpDesigner.Expressions.Evaluation;

/// <summary>Strategy for a fluent call such as <c>value.Format(...)</c> or <c>value.Currency()</c>.</summary>
public interface IExpressionFunction
{
    object? Invoke(object? target, IReadOnlyList<object?> arguments);
}
