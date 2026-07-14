namespace LabelSharpDesigner.Core.Styles;

public sealed record ShapeStyleSpec
{
    public ArgbColor BorderColor { get; init; } = ArgbColor.Black;
    public double BorderWidthMm { get; init; } = 0.3;
    public ArgbColor? FillColor { get; init; }

    public static readonly ShapeStyleSpec Default = new();
}
