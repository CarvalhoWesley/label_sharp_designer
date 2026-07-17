using LabelSharpDesignerCore.Core.Elements;
using SkiaSharp;

namespace LabelSharpDesignerCore.Rendering.Canvas;

internal static class ImageDrawing
{
    public static void Draw(SKCanvas canvas, SKRect bounds, string source, ImageFit fit)
    {
        using var bitmap = TryLoad(source);
        if (bitmap is null)
        {
            DrawPlaceholder(canvas, bounds);
            return;
        }

        var destination = ComputeDestination(bounds, bitmap.Width, bitmap.Height, fit);
        canvas.Save();
        canvas.ClipRect(bounds);
        canvas.DrawBitmap(bitmap, destination, SKSamplingOptions.Default);
        canvas.Restore();
    }

    private static SKBitmap? TryLoad(string source)
    {
        try
        {
            return File.Exists(source) ? SKBitmap.Decode(source) : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static SKRect ComputeDestination(SKRect bounds, int imageWidth, int imageHeight, ImageFit fit)
    {
        if (imageWidth <= 0 || imageHeight <= 0)
        {
            return bounds;
        }

        var boundsAspect = bounds.Width / bounds.Height;
        var imageAspect = (float)imageWidth / imageHeight;

        return fit switch
        {
            ImageFit.Fill => bounds,
            ImageFit.None => SKRect.Create(bounds.Left, bounds.Top, imageWidth, imageHeight),
            ImageFit.FitWidth => SKRect.Create(bounds.Left, bounds.Top, bounds.Width, bounds.Width / imageAspect),
            ImageFit.FitHeight => SKRect.Create(bounds.Left, bounds.Top, bounds.Height * imageAspect, bounds.Height),
            ImageFit.Cover => imageAspect > boundsAspect
                ? Centered(bounds, bounds.Height * imageAspect, bounds.Height)
                : Centered(bounds, bounds.Width, bounds.Width / imageAspect),
            _ => imageAspect > boundsAspect
                ? Centered(bounds, bounds.Width, bounds.Width / imageAspect)
                : Centered(bounds, bounds.Height * imageAspect, bounds.Height),
        };
    }

    private static SKRect Centered(SKRect bounds, float width, float height) => SKRect.Create(
        bounds.MidX - width / 2, bounds.MidY - height / 2, width, height);

    private static void DrawPlaceholder(SKCanvas canvas, SKRect bounds)
    {
        using var fill = new SKPaint { Color = new SKColor(230, 230, 230), Style = SKPaintStyle.Fill };
        using var border = new SKPaint { Color = new SKColor(160, 160, 160), Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        canvas.DrawRect(bounds, fill);
        canvas.DrawRect(bounds, border);
        canvas.DrawLine(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom, border);
        canvas.DrawLine(bounds.Right, bounds.Top, bounds.Left, bounds.Bottom, border);
    }
}
