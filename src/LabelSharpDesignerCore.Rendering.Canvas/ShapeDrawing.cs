using LabelSharpDesignerCore.Core.Layout;
using SkiaSharp;

namespace LabelSharpDesignerCore.Rendering.Canvas;

internal static class ShapeDrawing
{
    public static void Draw(SKCanvas canvas, SKRect bounds, ResolvedShapePayload payload)
    {
        if (payload.Kind == ShapeKind.Line)
        {
            using var linePaint = new SKPaint
            {
                Color = payload.StrokeColor.ToSkColor(),
                StrokeWidth = Math.Max((float)payload.StrokeWidthDots, 1f),
                IsAntialias = true,
            };
            canvas.DrawLine(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom, linePaint);
            return;
        }

        if (payload.FillColor is { } fill)
        {
            using var fillPaint = new SKPaint { Color = fill.ToSkColor(), Style = SKPaintStyle.Fill, IsAntialias = true };
            DrawGeometry(canvas, bounds, payload, fillPaint);
        }

        if (payload.StrokeWidthDots > 0)
        {
            using var strokePaint = new SKPaint
            {
                Color = payload.StrokeColor.ToSkColor(),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = (float)payload.StrokeWidthDots,
                IsAntialias = true,
            };
            DrawGeometry(canvas, bounds, payload, strokePaint);
        }
    }

    private static void DrawGeometry(SKCanvas canvas, SKRect bounds, ResolvedShapePayload payload, SKPaint paint)
    {
        switch (payload.Kind)
        {
            case ShapeKind.Rectangle when payload.CornerRadiusDots > 0:
                canvas.DrawRoundRect(bounds, (float)payload.CornerRadiusDots, (float)payload.CornerRadiusDots, paint);
                break;
            case ShapeKind.Rectangle:
                canvas.DrawRect(bounds, paint);
                break;
            case ShapeKind.Ellipse:
            case ShapeKind.Circle:
                canvas.DrawOval(bounds, paint);
                break;
        }
    }
}
