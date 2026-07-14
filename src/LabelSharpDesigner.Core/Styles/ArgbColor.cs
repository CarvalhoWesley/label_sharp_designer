using System.Globalization;

namespace LabelSharpDesigner.Core.Styles;

public readonly record struct ArgbColor(byte A, byte R, byte G, byte B)
{
    public static readonly ArgbColor Black = new(255, 0, 0, 0);
    public static readonly ArgbColor White = new(255, 255, 255, 255);
    public static readonly ArgbColor Transparent = new(0, 0, 0, 0);

    public string ToHex() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";

    public static ArgbColor FromHex(string hex)
    {
        var value = hex.StartsWith("#", StringComparison.Ordinal) ? hex.Substring(1) : hex;
        return value.Length switch
        {
            6 => new ArgbColor(
                255,
                byte.Parse(value.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(value.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(value.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)),
            8 => new ArgbColor(
                byte.Parse(value.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(value.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(value.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(value.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)),
            _ => throw new FormatException($"'{hex}' is not a valid #RRGGBB or #AARRGGBB color."),
        };
    }
}
