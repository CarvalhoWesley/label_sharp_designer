using LabelSharpDesigner.Expressions;

namespace LabelSharpDesigner.Layout;

public sealed class LayoutOptions
{
    /// <summary>Sample values keyed by root variable name, used to resolve {{ }} placeholders and expressions.</summary>
    public IReadOnlyDictionary<string, object?> SampleData { get; init; } = new Dictionary<string, object?>();

    public DateTimeOffset Now { get; init; } = DateTimeOffset.Now;

    public ExpressionEngine ExpressionEngine { get; init; } = new();
}
