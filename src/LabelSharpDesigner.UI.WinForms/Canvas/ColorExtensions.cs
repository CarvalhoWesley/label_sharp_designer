using LabelSharpDesigner.Core.Styles;
using SkiaSharp;

namespace LabelSharpDesigner.UI.WinForms.Canvas;

internal static class ColorExtensions
{
    public static SKColor ToSkColor(this ArgbColor color) => new(color.R, color.G, color.B, color.A);
}
