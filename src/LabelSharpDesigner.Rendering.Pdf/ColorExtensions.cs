using LabelSharpDesigner.Core.Styles;
using PdfSharp.Drawing;

namespace LabelSharpDesigner.Rendering.Pdf;

internal static class ColorExtensions
{
    public static XColor ToXColor(this ArgbColor color, double opacity = 1.0)
    {
        var clampedOpacity = opacity < 0 ? 0 : opacity > 1 ? 1 : opacity;
        var alpha = (byte)Math.Round(color.A * clampedOpacity);
        return XColor.FromArgb(alpha, color.R, color.G, color.B);
    }
}
