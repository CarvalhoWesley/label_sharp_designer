using LabelSharpDesignerCore.Expressions.Evaluation;

namespace LabelSharpDesignerCore.Expressions.Tests;

public class ExpressionEngineTests
{
    private static EvaluationContext SampleContext() => new(new Dictionary<string, object?>
    {
        ["produto"] = new Dictionary<string, object?>
        {
            ["nome"] = "Parafuso",
            ["preco"] = 12.5,
            ["ativo"] = true,
        },
        ["quantidade"] = 3,
    });

    private readonly ExpressionEngine _engine = new();

    [Theory]
    [InlineData("1 + 2", 3.0)]
    [InlineData("2 * (3 + 4)", 14.0)]
    [InlineData("10 / 4", 2.5)]
    [InlineData("10 % 3", 1.0)]
    [InlineData("-5 + 2", -3.0)]
    public void Evaluate_Arithmetic(string expression, double expected)
    {
        Assert.Equal(expected, _engine.Evaluate(expression, SampleContext()));
    }

    [Theory]
    [InlineData("1 < 2", true)]
    [InlineData("2 <= 2", true)]
    [InlineData("3 > 5", false)]
    [InlineData("3 == 3", true)]
    [InlineData("3 != 4", true)]
    [InlineData("true && false", false)]
    [InlineData("true || false", true)]
    [InlineData("!true", false)]
    public void Evaluate_ComparisonAndLogical(string expression, bool expected)
    {
        Assert.Equal(expected, _engine.Evaluate(expression, SampleContext()));
    }

    [Fact]
    public void Evaluate_Ternary()
    {
        Assert.Equal("caro", _engine.Evaluate("produto.preco > 10 ? 'caro' : 'barato'", SampleContext()));
        Assert.Equal("barato", _engine.Evaluate("produto.preco > 100 ? 'caro' : 'barato'", SampleContext()));
    }

    [Fact]
    public void Evaluate_PropertyAccess_ResolvesNestedDictionary()
    {
        Assert.Equal("Parafuso", _engine.Evaluate("produto.nome", SampleContext()));
        Assert.Equal(12.5, _engine.Evaluate("produto.preco", SampleContext()));
    }

    [Fact]
    public void Evaluate_FluentCall_InvokesRegisteredFunction()
    {
        var result = _engine.Evaluate("produto.preco.Currency()", SampleContext());

        Assert.Equal("R$ 12,50", result);
    }

    [Fact]
    public void Evaluate_UnknownVariable_Throws()
    {
        Assert.Throws<ExpressionEvaluationException>(() => _engine.Evaluate("inexistente.campo", SampleContext()));
    }

    [Fact]
    public void Evaluate_SyntaxError_ThrowsExpressionSyntaxException()
    {
        Assert.Throws<ExpressionSyntaxException>(() => _engine.Evaluate("1 + ", SampleContext()));
    }

    [Fact]
    public void Evaluate_CustomFunction_CanBeRegistered()
    {
        var registry = FunctionRegistry.CreateDefault();
        registry.Register("Shout", new DelegateFunction((target, _) => target?.ToString()?.ToUpperInvariant()));
        var engine = new ExpressionEngine(registry);

        Assert.Equal("PARAFUSO", engine.Evaluate("produto.nome.Shout()", SampleContext()));
    }

    private sealed class DelegateFunction : IExpressionFunction
    {
        private readonly Func<object?, IReadOnlyList<object?>, object?> _invoke;

        public DelegateFunction(Func<object?, IReadOnlyList<object?>, object?> invoke) => _invoke = invoke;

        public object? Invoke(object? target, IReadOnlyList<object?> arguments) => _invoke(target, arguments);
    }
}
