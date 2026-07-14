namespace LabelSharpDesigner.Core.Document;

public sealed record DocumentMetadata
{
    public string? Author { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public string? ThumbnailPngBase64 { get; init; }
    public IReadOnlyList<string> History { get; init; } = Array.Empty<string>();
}
