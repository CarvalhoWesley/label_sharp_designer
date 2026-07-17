using LabelSharpDesignerCore.Core.Elements;
using PdfSharp.Drawing;

namespace LabelSharpDesignerCore.Rendering.Pdf;

/// <summary>
/// Draws a file-backed <see cref="ImageElement"/>, or a gray placeholder when the source can't be
/// loaded. The returned <see cref="XImage"/> (if any) must be kept alive by the caller until the
/// <c>PdfDocument</c> is saved.
/// </summary>
internal static class ImageDrawing
{
    public static XImage? Draw(XGraphics gfx, XRect bounds, string source, ImageFit fit)
    {
        var image = TryLoad(source);
        if (image is null)
        {
            DrawPlaceholder(gfx, bounds);
            return null;
        }

        var destination = ComputeDestination(bounds, image.PixelWidth, image.PixelHeight, fit);
        var state = gfx.Save();
        gfx.IntersectClip(bounds);
        gfx.DrawImage(image, destination);
        gfx.Restore(state);
        return image;
    }

    private static XImage? TryLoad(string source)
    {
        try
        {
            return File.Exists(source) ? XImage.FromFile(source) : null;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException)
        {
            return null;
        }
    }

    private static XRect ComputeDestination(XRect bounds, int imageWidth, int imageHeight, ImageFit fit)
    {
        if (imageWidth <= 0 || imageHeight <= 0)
        {
            return bounds;
        }

        var boundsAspect = bounds.Width / bounds.Height;
        var imageAspect = (double)imageWidth / imageHeight;

        return fit switch
        {
            ImageFit.Fill => bounds,
            ImageFit.None => new XRect(bounds.Left, bounds.Top, imageWidth, imageHeight),
            ImageFit.FitWidth => new XRect(bounds.Left, bounds.Top, bounds.Width, bounds.Width / imageAspect),
            ImageFit.FitHeight => new XRect(bounds.Left, bounds.Top, bounds.Height * imageAspect, bounds.Height),
            ImageFit.Cover => imageAspect > boundsAspect
                ? Centered(bounds, bounds.Height * imageAspect, bounds.Height)
                : Centered(bounds, bounds.Width, bounds.Width / imageAspect),
            _ => imageAspect > boundsAspect
                ? Centered(bounds, bounds.Width, bounds.Width / imageAspect)
                : Centered(bounds, bounds.Height * imageAspect, bounds.Height),
        };
    }

    private static XRect Centered(XRect bounds, double width, double height) => new(
        bounds.Left + bounds.Width / 2 - width / 2, bounds.Top + bounds.Height / 2 - height / 2, width, height);

    private static void DrawPlaceholder(XGraphics gfx, XRect bounds)
    {
        var fill = new XSolidBrush(XColor.FromArgb(230, 230, 230));
        var border = new XPen(XColor.FromArgb(160, 160, 160), 1);
        gfx.DrawRectangle(fill, bounds);
        gfx.DrawRectangle(border, bounds);
        gfx.DrawLine(border, bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
        gfx.DrawLine(border, bounds.Right, bounds.Top, bounds.Left, bounds.Bottom);
    }
}
