using LabelSharpDesigner.Core.Elements;

namespace LabelSharpDesigner.Core.Layout;

public sealed record ResolvedElement
{
    public required string SourceElementId { get; init; }
    public required int XDots { get; init; }
    public required int YDots { get; init; }
    public required int WidthDots { get; init; }
    public required int HeightDots { get; init; }
    public required double RotationDegrees { get; init; }
    public required int ZIndex { get; init; }
    public required double Opacity { get; init; }
    public ElementTransform Transform { get; init; } = ElementTransform.Identity;
    public required ResolvedPayload Payload { get; init; }
}
