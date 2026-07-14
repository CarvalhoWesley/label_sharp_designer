using LabelSharpDesigner.Core.Styles;
using PdfSharp.Drawing;

namespace LabelSharpDesigner.Rendering.Pdf;

internal static class TextDrawing
{
    public static void Draw(XGraphics gfx, XRect bounds, string text, TextStyleSpec style, double opacity, int dpi)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        // PdfExporter scales the whole XGraphics context from document dots to PDF points once,
        // so every coordinate this renderer touches — including font size — must stay expressed
        // in dots for the CTM to produce the intended physical size.
        var pointsToDots = dpi / 72.0;

        var fontStyle = XFontStyleEx.Regular;
        if (style.Bold)
        {
            fontStyle |= XFontStyleEx.Bold;
        }

        if (style.Italic)
        {
            fontStyle |= XFontStyleEx.Italic;
        }

        if (style.Underline)
        {
            fontStyle |= XFontStyleEx.Underline;
        }

        var font = new XFont(style.FontFamily, style.FontSizePt * pointsToDots, fontStyle);
        var brush = new XSolidBrush(style.Color.ToXColor(opacity));

        var format = new XStringFormat
        {
            Alignment = style.Align switch
            {
                TextAlign.Center => XStringAlignment.Center,
                TextAlign.Right => XStringAlignment.Far,
                _ => XStringAlignment.Near,
            },
            LineAlignment = XLineAlignment.Near,
        };

        // DrawString(..., XRect, ...) only positions/aligns a single line within the rect — it has no
        // built-in auto-wrap — so lines are measured and broken here, then each one is drawn into its
        // own single-line slice of the box at an incrementing Y.
        var lineHeight = font.Height;
        var y = bounds.Top;
        foreach (var line in WrapText(gfx, text, font, bounds.Width))
        {
            var lineRect = new XRect(bounds.Left, y, bounds.Width, lineHeight);
            gfx.DrawString(line, font, brush, lineRect, format);
            y += lineHeight;
        }
    }

    /// <summary>Greedy word-wrap: each explicit "\n"-delimited paragraph is broken into as many lines
    /// as fit <paramref name="maxWidth"/>, breaking on spaces. A single word wider than
    /// <paramref name="maxWidth"/> on its own is kept whole on its own line (never split mid-word)
    /// rather than silently overflowing past the element or being dropped. Vertical overflow (more
    /// wrapped lines than fit the element's height) is not clipped here — same as the previous
    /// single-line behavior, which also let text overflow rather than truncating it.</summary>
    private static List<string> WrapText(XGraphics gfx, string text, XFont font, double maxWidth)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Split('\n'))
        {
            if (maxWidth <= 0)
            {
                lines.Add(paragraph);
                continue;
            }

            var words = paragraph.Split(' ');
            var currentLine = string.Empty;
            foreach (var word in words)
            {
                var candidate = currentLine.Length == 0 ? word : $"{currentLine} {word}";
                if (currentLine.Length == 0 || gfx.MeasureString(candidate, font).Width <= maxWidth)
                {
                    currentLine = candidate;
                }
                else
                {
                    lines.Add(currentLine);
                    currentLine = word;
                }
            }

            lines.Add(currentLine);
        }

        return lines;
    }
}
