using LabelSharpDesignerCore.Core.Elements;
using SkiaSharp;

namespace LabelSharpDesignerCore.UI.WinForms.Canvas;

/// <summary>mm-space geometry helpers shared by hit-testing, selection outlines and resize/rotate handles.</summary>
internal static class ElementGeometry
{
    public static SKRect BoundsMm(LabelElement element) => SKRect.Create(
        (float)element.Position.X, (float)element.Position.Y, (float)element.Size.Width, (float)element.Size.Height);

    public static SKPoint CenterMm(LabelElement element)
    {
        var bounds = BoundsMm(element);
        return new SKPoint(bounds.MidX, bounds.MidY);
    }

    /// <summary>The four corners of an element's bounds, rotated around its center.</summary>
    public static SKPoint[] RotatedCorners(LabelElement element)
    {
        var bounds = BoundsMm(element);
        var center = new SKPoint(bounds.MidX, bounds.MidY);
        SKPoint[] corners =
        [
            new(bounds.Left, bounds.Top),
            new(bounds.Right, bounds.Top),
            new(bounds.Right, bounds.Bottom),
            new(bounds.Left, bounds.Bottom),
        ];

        return element.RotationDegrees == 0
            ? corners
            : Array.ConvertAll(corners, corner => RotatePoint(corner, center, element.RotationDegrees));
    }

    public static SKPoint RotatePoint(SKPoint point, SKPoint center, double degrees)
    {
        var radians = degrees * Math.PI / 180;
        var cos = (float)Math.Cos(radians);
        var sin = (float)Math.Sin(radians);
        var dx = point.X - center.X;
        var dy = point.Y - center.Y;
        return new SKPoint(center.X + dx * cos - dy * sin, center.Y + dx * sin + dy * cos);
    }

    /// <summary>True if <paramref name="pointMm"/> falls within the element's (possibly rotated) bounds.</summary>
    public static bool HitTest(LabelElement element, SKPoint pointMm)
    {
        var bounds = BoundsMm(element);
        var center = new SKPoint(bounds.MidX, bounds.MidY);
        var local = element.RotationDegrees == 0 ? pointMm : RotatePoint(pointMm, center, -element.RotationDegrees);
        return bounds.Contains(local);
    }

    public static SKRect CombinedBoundsMm(IEnumerable<LabelElement> elements)
    {
        SKRect? combined = null;
        foreach (var element in elements)
        {
            var corners = RotatedCorners(element);
            foreach (var corner in corners)
            {
                combined = combined is { } existing
                    ? SKRect.Union(existing, SKRect.Create(corner.X, corner.Y, 0, 0))
                    : SKRect.Create(corner.X, corner.Y, 0, 0);
            }
        }

        return combined ?? SKRect.Empty;
    }
}
