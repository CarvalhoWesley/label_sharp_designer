using System.Globalization;

namespace LabelSharpDesignerCore.Expressions.Evaluation.Functions;

/// <summary><c>value.Format("dd/MM/yyyy")</c> for dates, or a .NET numeric format string for numbers.</summary>
public sealed class FormatFunction : IExpressionFunction
{
    public object? Invoke(object? target, IReadOnlyList<object?> arguments)
    {
        var format = arguments.Count > 0 ? arguments[0]?.ToString() : null;

        return target switch
        {
            null => null,
            DateTime dt when format is not null => dt.ToString(format, CultureInfo.InvariantCulture),
            DateTimeOffset dto when format is not null => dto.ToString(format, CultureInfo.InvariantCulture),
            IFormattable formattable when format is not null => formattable.ToString(format, CultureInfo.InvariantCulture),
            _ => target.ToString(),
        };
    }
}
