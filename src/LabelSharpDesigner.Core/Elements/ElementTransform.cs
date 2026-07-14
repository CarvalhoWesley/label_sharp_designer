namespace LabelSharpDesigner.Core.Elements;

public sealed record ElementTransform
{
    public bool FlipH { get; init; }
    public bool FlipV { get; init; }
    public double SkewX { get; init; }
    public double SkewY { get; init; }

    public static readonly ElementTransform Identity = new();
}
