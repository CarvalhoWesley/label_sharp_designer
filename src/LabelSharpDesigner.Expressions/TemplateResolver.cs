using System.Text;
using LabelSharpDesigner.Expressions.Evaluation;

namespace LabelSharpDesigner.Expressions;

/// <summary>Resolves <c>{{ expression }}</c> placeholders embedded in literal text.</summary>
public sealed class TemplateResolver
{
    private readonly ExpressionEngine _engine;

    public TemplateResolver(ExpressionEngine? engine = null)
    {
        _engine = engine ?? new ExpressionEngine();
    }

    public string Resolve(string template, EvaluationContext context)
    {
        var result = new StringBuilder();
        var i = 0;

        while (i < template.Length)
        {
            var start = template.IndexOf("{{", i, StringComparison.Ordinal);
            if (start < 0)
            {
                result.Append(template, i, template.Length - i);
                break;
            }

            result.Append(template, i, start - i);

            var end = template.IndexOf("}}", start + 2, StringComparison.Ordinal);
            if (end < 0)
            {
                result.Append(template, start, template.Length - start);
                break;
            }

            var expression = template.Substring(start + 2, end - start - 2).Trim();
            result.Append(_engine.EvaluateToDisplayString(expression, context));

            i = end + 2;
        }

        return result.ToString();
    }
}
