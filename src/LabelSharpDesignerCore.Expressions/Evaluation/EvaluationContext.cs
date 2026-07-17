namespace LabelSharpDesignerCore.Expressions.Evaluation;

public sealed class EvaluationContext
{
    private readonly IReadOnlyDictionary<string, object?> _values;

    public EvaluationContext(IReadOnlyDictionary<string, object?> values)
    {
        _values = values;
    }

    public static readonly EvaluationContext Empty = new(new Dictionary<string, object?>());

    public bool TryGetRoot(string name, out object? value) => _values.TryGetValue(name, out value);
}
