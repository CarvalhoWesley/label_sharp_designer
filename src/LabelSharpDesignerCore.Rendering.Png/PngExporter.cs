using LabelSharpDesignerCore.Core.Layout;
using LabelSharpDesignerCore.Rendering.Canvas;
using SkiaSharp;

namespace LabelSharpDesignerCore.Rendering.Png;

public static class PngExporter
{
    public static byte[] Export(ResolvedDocument document, PngScale scale = PngScale.X1)
    {
        var factor = (int)scale;
        var width = document.WidthDots * factor;
        var height = document.HeightDots * factor;

        return Render(document, width, height, (float)factor);
    }

    /// <summary>Renders at whatever scale fits <paramref name="targetWidthPx"/> — used for library
    /// thumbnails, where the source document's DPI/size varies but the output should stay a
    /// consistent width.</summary>
    public static byte[] ExportScaled(ResolvedDocument document, int targetWidthPx)
    {
        var scale = document.WidthDots <= 0 ? 1f : (float)targetWidthPx / document.WidthDots;
        var width = Math.Max(1, (int)Math.Round(document.WidthDots * scale));
        var height = Math.Max(1, (int)Math.Round(document.HeightDots * scale));

        return Render(document, width, height, scale);
    }

    private static byte[] Render(ResolvedDocument document, int width, int height, float scale)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;

        canvas.Scale(scale, scale);
        LabelCanvasRenderer.Render(canvas, document);
        canvas.Flush();

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, quality: 100);
        return data.ToArray();
    }
}
