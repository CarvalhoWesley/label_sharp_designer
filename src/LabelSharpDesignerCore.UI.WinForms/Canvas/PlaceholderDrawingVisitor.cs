using LabelSharpDesignerCore.Core.Elements;
using LabelSharpDesignerCore.Core.Styles;
using SkiaSharp;

namespace LabelSharpDesignerCore.UI.WinForms.Canvas;

/// <summary>
/// Draws a simplified placeholder for each element directly from the domain model — the
/// interactive canvas never runs the Layout/Rendering pipeline (that stays reserved for the
/// preview tab and PNG/PDF export, so canvas redraws during drag/resize stay cheap).
/// </summary>
internal sealed class PlaceholderDrawingVisitor : IElementVisitor<object?>
{
    private static readonly SKColor PlaceholderFill = new(0xF0, 0xF0, 0xF0);
    private static readonly SKColor PlaceholderBorder = new(0x90, 0x90, 0x90);
    private static readonly SKColor DataFill = new(0xE3, 0xEF, 0xFB);

    private readonly SKCanvas _canvas;
    private readonly CanvasTransform _transform;

    public PlaceholderDrawingVisitor(SKCanvas canvas, CanvasTransform transform)
    {
        _canvas = canvas;
        _transform = transform;
    }

    public object? VisitText(TextElement element) => WithElementTransform(element, bounds =>
        DrawLabeledBox(bounds, string.IsNullOrEmpty(element.Content) ? "Text" : element.Content, PlaceholderFill, element.Style.Color.ToSkColor(), element.Style, wrap: true));

    public object? VisitBarcode(BarcodeElement element) => WithElementTransform(element, bounds =>
        DrawLabeledBox(bounds, $"|||  {element.Symbology}  |||", DataFill, SKColors.Black));

    public object? VisitQrCode(QrCodeElement element) => WithElementTransform(element, bounds =>
        DrawLabeledBox(bounds, "QR", DataFill, SKColors.Black));

    public object? VisitImage(ImageElement element) => WithElementTransform(element, bounds =>
        DrawLabeledBox(bounds, string.IsNullOrEmpty(element.Source) ? "Image" : Path.GetFileName(element.Source), PlaceholderFill, PlaceholderBorder));

    public object? VisitRectangle(RectangleElement element) => WithElementTransform(element, bounds => DrawShape(bounds, element.Style, element.CornerRadius));

    public object? VisitEllipse(EllipseElement element) => WithElementTransform(element, bounds => DrawOval(bounds, element.Style));

    public object? VisitCircle(CircleElement element) => WithElementTransform(element, bounds => DrawOval(bounds, element.Style));

    public object? VisitLine(LineElement element) => WithElementTransform(element, bounds =>
    {
        using var paint = new SKPaint { Color = element.StrokeColor.ToSkColor(), StrokeWidth = Math.Max(_transform.LengthToPixels((float)element.StrokeWidth), 1), IsAntialias = true };
        _canvas.DrawLine(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom, paint);
    });

    public object? VisitVariable(VariableElement element) => WithElementTransform(element, bounds =>
        DrawLabeledBox(bounds, $"{{{{{element.Expression}}}}}", DataFill, element.Style.Color.ToSkColor(), element.Style, wrap: true));

    public object? VisitDate(DateElement element) => WithElementTransform(element, bounds =>
        DrawLabeledBox(bounds, string.IsNullOrEmpty(element.Format) ? "Date" : element.Format, PlaceholderFill, element.Style.Color.ToSkColor(), element.Style, wrap: true));

    public object? VisitTime(TimeElement element) => WithElementTransform(element, bounds =>
        DrawLabeledBox(bounds, string.IsNullOrEmpty(element.Format) ? "Time" : element.Format, PlaceholderFill, element.Style.Color.ToSkColor(), element.Style, wrap: true));

    public object? VisitTable(TableElement element) => WithElementTransform(element, bounds =>
    {
        DrawLabeledBox(bounds, $"Table ({element.Columns.Count} cols)", PlaceholderFill, PlaceholderBorder);
        if (element.Columns.Count > 1)
        {
            using var paint = new SKPaint { Color = PlaceholderBorder, StrokeWidth = 1 };
            var columnWidth = bounds.Width / element.Columns.Count;
            for (var i = 1; i < element.Columns.Count; i++)
            {
                var x = bounds.Left + i * columnWidth;
                _canvas.DrawLine(x, bounds.Top, x, bounds.Bottom, paint);
            }
        }
    });

    public object? VisitGroup(GroupElement element)
    {
        WithElementTransform(element, bounds =>
        {
            using var paint = new SKPaint { Color = PlaceholderBorder, Style = SKPaintStyle.Stroke, StrokeWidth = 1, PathEffect = SKPathEffect.CreateDash([4, 3], 0) };
            _canvas.DrawRect(bounds, paint);
        });

        foreach (var child in element.Children)
        {
            child.Accept(this);
        }

        return null;
    }

    private object? WithElementTransform(LabelElement element, Action<SKRect> draw)
    {
        var boundsPx = _transform.BoundsToPixels(ElementGeometry.BoundsMm(element));

        _canvas.Save();
        if (element.RotationDegrees != 0)
        {
            _canvas.RotateDegrees((float)element.RotationDegrees, boundsPx.MidX, boundsPx.MidY);
        }

        draw(boundsPx);
        _canvas.Restore();
        return null;
    }

    /// <summary>Millimeters per point (1 pt = 1/72 in) — used to convert a <see cref="TextStyleSpec.FontSizePt"/>
    /// into the same mm-based unit every other on-canvas length (stroke width, corner radius, ...)
    /// is scaled through, so text placeholders resize with both the style's font size and the
    /// current zoom level.</summary>
    private const float MmPerPoint = 25.4f / 72f;

    private void DrawLabeledBox(SKRect bounds, string label, SKColor fill, SKColor textColor, TextStyleSpec? style = null, bool wrap = false)
    {
        using var fillPaint = new SKPaint { Color = fill, Style = SKPaintStyle.Fill };
        using var borderPaint = new SKPaint { Color = PlaceholderBorder, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        _canvas.DrawRect(bounds, fillPaint);
        _canvas.DrawRect(bounds, borderPaint);

        // Placeholder text still isn't the real renderer (see class doc), but the size/weight should
        // at least track the element's own style instead of a fixed 11px — otherwise editing "Tamanho
        // (pt)" in the property panel only ever showed up in the Pré-visualizar tab, never here.
        var fontSizePx = style is null ? 11f : Math.Max(_transform.LengthToPixels((float)style.FontSizePt * MmPerPoint), 6f);
        var typeface = style is { Bold: true, Italic: true }
            ? SKTypeface.FromFamilyName(null, SKFontStyle.BoldItalic)
            : style is { Bold: true }
                ? SKTypeface.FromFamilyName(null, SKFontStyle.Bold)
                : style is { Italic: true }
                    ? SKTypeface.FromFamilyName(null, SKFontStyle.Italic)
                    : SKTypeface.Default;

        using var font = new SKFont(typeface, fontSizePx);
        using var textPaint = new SKPaint { Color = textColor, IsAntialias = true };
        _canvas.Save();
        _canvas.ClipRect(bounds);

        if (wrap)
        {
            var lineHeight = font.Metrics.Descent - font.Metrics.Ascent + font.Metrics.Leading;
            var y = bounds.Top + fontSizePx;
            foreach (var line in WrapPlaceholderText(label, font, textPaint, bounds.Width - 6))
            {
                _canvas.DrawText(line, bounds.Left + 3, y, SKTextAlign.Left, font, textPaint);
                y += lineHeight;
            }
        }
        else
        {
            _canvas.DrawText(label, bounds.Left + 3, bounds.Top + fontSizePx, SKTextAlign.Left, font, textPaint);
        }

        _canvas.Restore();
    }

    /// <summary>Same greedy word-wrap as <c>Rendering.Canvas/TextDrawing.WrapText</c>, duplicated here
    /// rather than shared — this placeholder deliberately never calls into the real Layout/Rendering
    /// pipeline (see class doc), so its wrap has to be its own simplified copy, not a reused one.</summary>
    private static List<string> WrapPlaceholderText(string text, SKFont font, SKPaint paint, float maxWidth)
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

    private void DrawShape(SKRect bounds, ShapeStyleSpec style, double cornerRadius)
    {
        if (style.FillColor is { } fill)
        {
            using var fillPaint = new SKPaint { Color = fill.ToSkColor(), Style = SKPaintStyle.Fill, IsAntialias = true };
            DrawRectGeometry(bounds, cornerRadius, fillPaint);
        }

        using var strokePaint = new SKPaint
        {
            Color = style.BorderColor.ToSkColor(),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(_transform.LengthToPixels((float)style.BorderWidthMm), 1),
            IsAntialias = true,
        };
        DrawRectGeometry(bounds, cornerRadius, strokePaint);
    }

    private void DrawRectGeometry(SKRect bounds, double cornerRadius, SKPaint paint)
    {
        var radiusPx = _transform.LengthToPixels((float)cornerRadius);
        if (radiusPx > 0)
        {
            _canvas.DrawRoundRect(bounds, radiusPx, radiusPx, paint);
        }
        else
        {
            _canvas.DrawRect(bounds, paint);
        }
    }

    private void DrawOval(SKRect bounds, ShapeStyleSpec style)
    {
        if (style.FillColor is { } fill)
        {
            using var fillPaint = new SKPaint { Color = fill.ToSkColor(), Style = SKPaintStyle.Fill, IsAntialias = true };
            _canvas.DrawOval(bounds, fillPaint);
        }

        using var strokePaint = new SKPaint
        {
            Color = style.BorderColor.ToSkColor(),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(_transform.LengthToPixels((float)style.BorderWidthMm), 1),
            IsAntialias = true,
        };
        _canvas.DrawOval(bounds, strokePaint);
    }
}
