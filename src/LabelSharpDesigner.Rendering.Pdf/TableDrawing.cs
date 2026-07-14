using LabelSharpDesigner.Core.Layout;
using PdfSharp.Drawing;

namespace LabelSharpDesigner.Rendering.Pdf;

/// <summary>Draws only the table's outer frame, header divider and column headers — mirrors
/// <c>Rendering.Canvas</c>'s <c>TableDrawing</c> scope decision (cell/row data binding deferred).</summary>
internal static class TableDrawing
{
    public static void Draw(XGraphics gfx, XRect bounds, ResolvedTablePayload payload, int dpi)
    {
        var border = new XPen(XColors.Black, 1);
        gfx.DrawRectangle(border, bounds);

        if (payload.Columns.Count == 0)
        {
            return;
        }

        var headerBottom = bounds.Top + payload.RowHeightDots;
        gfx.DrawLine(border, bounds.Left, headerBottom, bounds.Right, headerBottom);

        var columnWidth = bounds.Width / payload.Columns.Count;
        for (var i = 0; i < payload.Columns.Count; i++)
        {
            var columnLeft = bounds.Left + i * columnWidth;

            if (i > 0)
            {
                gfx.DrawLine(border, columnLeft, bounds.Top, columnLeft, bounds.Bottom);
            }

            var headerBounds = new XRect(columnLeft, bounds.Top, columnWidth, headerBottom - bounds.Top);
            TextDrawing.Draw(gfx, headerBounds, payload.Columns[i].Header, payload.HeaderStyle, 1.0, dpi);
        }
    }
}
