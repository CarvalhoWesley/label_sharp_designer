using System.Globalization;
using LabelSharpDesignerCore.Expressions.Evaluation;
using LabelSharpDesignerCore.Expressions.Parsing;

namespace LabelSharpDesignerCore.Expressions;

public sealed class ExpressionEngine
{
    private readonly FunctionRegistry _functions;

    public ExpressionEngine(FunctionRegistry? functions = null)
    {
        _functions = functions ?? FunctionRegistry.CreateDefault();
    }

    public object? Evaluate(string expression, EvaluationContext context)
    {
        var ast = Parser.Parse(expression);
        return new Evaluator(_functions).Evaluate(ast, context);
    }

    public string EvaluateToDisplayString(string expression, EvaluationContext context)
    {
        var result = Evaluate(expression, context);
        return result switch
        {
            null => string.Empty,
            double d => d.ToString(CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => result.ToString() ?? string.Empty,
        };
    }
}
