using LabelSharpDesigner.Core.Elements;

namespace LabelSharpDesigner.Core.Document;

public sealed record LabelDocument
{
    public const int CurrentSchemaVersion = 1;

    public int Version { get; init; } = CurrentSchemaVersion;
    public required string Name { get; init; }
    public required PageConfig Page { get; init; }
    public IReadOnlyList<LabelLayer> Layers { get; init; } = Array.Empty<LabelLayer>();
    public IReadOnlyList<LabelStyle> Styles { get; init; } = Array.Empty<LabelStyle>();
    public IReadOnlyList<LabelVariable> Variables { get; init; } = Array.Empty<LabelVariable>();
    public IReadOnlyList<LabelElement> Elements { get; init; } = Array.Empty<LabelElement>();
    public EditorSettings EditorSettings { get; init; } = EditorSettings.Default;
    public DocumentMetadata Metadata { get; init; } = new()
    {
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };
}
