using LabelSharpDesigner.Core.Layout;
using SkiaSharp;

namespace LabelSharpDesigner.Rendering.Canvas;

/// <summary>
/// Draws only the table's outer frame, header divider and column headers. Cell/row data binding
/// is deferred, mirroring the original project's own scope decision for <c>TableElement</c>.
/// </summary>
internal static class TableDrawing
{
    public static void Draw(SKCanvas canvas, SKRect bounds, ResolvedTablePayload payload, int dpi)
    {
        using var border = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
        canvas.DrawRect(bounds, border);

        if (payload.Columns.Count == 0)
        {
            return;
        }

        var headerBottom = bounds.Top + (float)payload.RowHeightDots;
        canvas.DrawLine(bounds.Left, headerBottom, bounds.Right, headerBottom, border);

        var columnWidth = bounds.Width / payload.Columns.Count;
        for (var i = 0; i < payload.Columns.Count; i++)
        {
            var columnLeft = bounds.Left + i * columnWidth;

            if (i > 0)
            {
                canvas.DrawLine(columnLeft, bounds.Top, columnLeft, bounds.Bottom, border);
            }

            var headerBounds = new SKRect(columnLeft, bounds.Top, columnLeft + columnWidth, headerBottom);
            TextDrawing.Draw(canvas, headerBounds, payload.Columns[i].Header, payload.HeaderStyle, dpi);
        }
    }
}
