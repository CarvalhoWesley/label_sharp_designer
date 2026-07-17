using LabelSharpDesignerCore.Core.Layout;
using PdfSharp.Drawing;

namespace LabelSharpDesignerCore.Rendering.Pdf;

internal static class ShapeDrawing
{
    public static void Draw(XGraphics gfx, XRect bounds, ResolvedShapePayload payload, double opacity)
    {
        if (payload.Kind == ShapeKind.Line)
        {
            var linePen = new XPen(payload.StrokeColor.ToXColor(opacity), Math.Max(payload.StrokeWidthDots, 1));
            gfx.DrawLine(linePen, bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
            return;
        }

        if (payload.FillColor is { } fill)
        {
            var fillBrush = new XSolidBrush(fill.ToXColor(opacity));
            DrawFilled(gfx, bounds, payload, fillBrush);
        }

        if (payload.StrokeWidthDots > 0)
        {
            var strokePen = new XPen(payload.StrokeColor.ToXColor(opacity), payload.StrokeWidthDots);
            DrawStroked(gfx, bounds, payload, strokePen);
        }
    }

    private static void DrawFilled(XGraphics gfx, XRect bounds, ResolvedShapePayload payload, XBrush brush)
    {
        switch (payload.Kind)
        {
            case ShapeKind.Rectangle when payload.CornerRadiusDots > 0:
                gfx.DrawRoundedRectangle(brush, bounds, new XSize(payload.CornerRadiusDots, payload.CornerRadiusDots));
                break;
            case ShapeKind.Rectangle:
                gfx.DrawRectangle(brush, bounds);
                break;
            case ShapeKind.Ellipse or ShapeKind.Circle:
                gfx.DrawEllipse(brush, bounds);
                break;
        }
    }

    private static void DrawStroked(XGraphics gfx, XRect bounds, ResolvedShapePayload payload, XPen pen)
    {
        switch (payload.Kind)
        {
            case ShapeKind.Rectangle when payload.CornerRadiusDots > 0:
                gfx.DrawRoundedRectangle(pen, bounds, new XSize(payload.CornerRadiusDots, payload.CornerRadiusDots));
                break;
            case ShapeKind.Rectangle:
                gfx.DrawRectangle(pen, bounds);
                break;
            case ShapeKind.Ellipse or ShapeKind.Circle:
                gfx.DrawEllipse(pen, bounds);
                break;
        }
    }
}
