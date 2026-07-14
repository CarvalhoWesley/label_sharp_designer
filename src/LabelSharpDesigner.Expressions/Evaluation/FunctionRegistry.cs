using LabelSharpDesigner.Expressions.Evaluation.Functions;

namespace LabelSharpDesigner.Expressions.Evaluation;

public sealed class FunctionRegistry
{
    private readonly Dictionary<string, IExpressionFunction> _functions;

    public FunctionRegistry(IDictionary<string, IExpressionFunction>? functions = null)
    {
        _functions = functions is null
            ? new Dictionary<string, IExpressionFunction>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, IExpressionFunction>(functions, StringComparer.OrdinalIgnoreCase);
    }

    public static FunctionRegistry CreateDefault() => new(new Dictionary<string, IExpressionFunction>(StringComparer.OrdinalIgnoreCase)
    {
        ["Format"] = new FormatFunction(),
        ["Currency"] = new CurrencyFunction(),
    });

    public void Register(string name, IExpressionFunction function) => _functions[name] = function;

    public bool TryGet(string name, out IExpressionFunction function) => _functions.TryGetValue(name, out function!);
}
