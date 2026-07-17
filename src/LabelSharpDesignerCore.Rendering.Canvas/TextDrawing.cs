using LabelSharpDesignerCore.Core.Styles;
using SkiaSharp;

namespace LabelSharpDesignerCore.Rendering.Canvas;

internal static class TextDrawing
{
    public static void Draw(SKCanvas canvas, SKRect bounds, string text, TextStyleSpec style, int dpi)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var pointsToDots = dpi / 72f;
        var weight = style.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
        var slant = style.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
        using var typeface = SKTypeface.FromFamilyName(style.FontFamily, weight, SKFontStyleWidth.Normal, slant);
        using var font = new SKFont(typeface, (float)style.FontSizePt * pointsToDots);
        using var paint = new SKPaint { Color = style.Color.ToSkColor(), IsAntialias = true };

        var (align, x) = style.Align switch
        {
            TextAlign.Center => (SKTextAlign.Center, bounds.MidX),
            TextAlign.Right => (SKTextAlign.Right, bounds.Right),
            TextAlign.Justify => (SKTextAlign.Left, bounds.Left),
            _ => (SKTextAlign.Left, bounds.Left),
        };

        var lineHeight = font.Metrics.Descent - font.Metrics.Ascent + font.Metrics.Leading;
        var y = bounds.Top - font.Metrics.Ascent;

        foreach (var line in WrapText(text, font, paint, bounds.Width))
        {
            canvas.DrawText(line, x, y, align, font, paint);

            if (style.Underline)
            {
                var width = font.MeasureText(line, out _, paint);
                var underlineY = y - font.Metrics.UnderlinePosition ?? y + 1;
                var startX = align switch
                {
                    SKTextAlign.Center => x - width / 2,
                    SKTextAlign.Right => x - width,
                    _ => x,
                };

                using var linePaint = new SKPaint
                {
                    Color = style.Color.ToSkColor(),
                    StrokeWidth = font.Metrics.UnderlineThickness ?? 1f,
                    IsAntialias = true,
                };
                canvas.DrawLine(startX, underlineY, startX + width, underlineY, linePaint);
            }

            y += lineHeight;
        }
    }

    /// <summary>Greedy word-wrap: each explicit "\n"-delimited paragraph is broken into as many lines
    /// as fit <paramref name="maxWidth"/>, breaking on spaces. A single word wider than
    /// <paramref name="maxWidth"/> on its own is kept whole on its own line (never split mid-word)
    /// rather than silently overflowing past the element or being dropped. Vertical overflow (more
    /// wrapped lines than fit the element's height) is not clipped here — same as the previous
    /// single-line behavior, which also let text overflow rather than truncating it.</summary>
    private static List<string> WrapText(string text, SKFont font, SKPaint paint, float maxWidth)
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
                if (currentLine.Length == 0 || font.MeasureText(candidate, out _, paint) <= maxWidth)
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
