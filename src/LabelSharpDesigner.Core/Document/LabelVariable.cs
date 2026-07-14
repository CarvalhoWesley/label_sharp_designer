namespace LabelSharpDesigner.Core.Document;

public enum VariableValueType
{
    String,
    Number,
    Date,
    Boolean,
}

public sealed record LabelVariable
{
    public required string Name { get; init; }
    public VariableValueType Type { get; init; } = VariableValueType.String;
    public string? DefaultValue { get; init; }
    public string? Description { get; init; }
}
