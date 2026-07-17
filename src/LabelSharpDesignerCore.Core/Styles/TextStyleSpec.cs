namespace LabelSharpDesignerCore.Core.Styles;

public enum TextAlign
{
    Left,
    Center,
    Right,
    Justify,
}

public sealed record TextStyleSpec
{
    public string FontFamily { get; init; } = "Arial";
    public double FontSizePt { get; init; } = 10;
    public bool Bold { get; init; }
    public bool Italic { get; init; }
    public bool Underline { get; init; }
    public ArgbColor Color { get; init; } = ArgbColor.Black;
    public TextAlign Align { get; init; } = TextAlign.Left;

    public static readonly TextStyleSpec Default = new();
}
