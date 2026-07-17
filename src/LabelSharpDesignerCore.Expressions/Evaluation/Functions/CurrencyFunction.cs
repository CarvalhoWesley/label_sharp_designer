using System.Globalization;

namespace LabelSharpDesignerCore.Expressions.Evaluation.Functions;

/// <summary><c>value.Currency()</c> — formats a numeric value using a currency format.</summary>
public sealed class CurrencyFunction : IExpressionFunction
{
    private readonly CultureInfo _culture;

    public CurrencyFunction(CultureInfo? culture = null)
    {
        _culture = culture ?? CultureInfo.GetCultureInfo("pt-BR");
    }

    public object? Invoke(object? target, IReadOnlyList<object?> arguments)
    {
        var number = target switch
        {
            null => (double?)null,
            double d => d,
            int i => i,
            decimal m => (double)m,
            string s when double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => (double?)null,
        };

        return number is null ? target?.ToString() : number.Value.ToString("C", _culture);
    }
}
