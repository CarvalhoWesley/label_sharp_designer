using System.Runtime.InteropServices;
using LabelSharpDesignerCore.Barcode;
using LabelSharpDesignerCore.Core.Layout;
using PdfSharp.Drawing;
using SkiaSharp;

namespace LabelSharpDesignerCore.Rendering.Pdf;

/// <summary>
/// Barcodes and QR codes are embedded as raster PNG images (reusing the same ZXing.Net-backed
/// <see cref="BarcodeGenerator"/> as the canvas/PNG renderers), not drawn as vector bars — ZXing.Net
/// only exposes a rasterized module buffer through its public API, so true vector output would mean
/// re-implementing each symbology's bar-pattern algorithm independently. At typical label print
/// resolutions (200-600 dpi) a raster barcode/QR is still fully scannable, and this keeps a single
/// source of truth for barcode rendering shared with <c>Rendering.Canvas</c>/<c>Rendering.Png</c>.
/// The returned <see cref="XImage"/> must be kept alive (and eventually disposed) by the caller
/// until the <c>PdfDocument</c> is saved.
/// </summary>
internal static class BarcodeDrawing
{
    public static XImage DrawBarcode(XGraphics gfx, XRect bounds, ResolvedBarcodePayload payload, int dpi)
    {
        var barsBounds = bounds;
        XRect? textBounds = null;

        if (payload.ShowText)
        {
            // Reserve a strip at the bottom of the element for the human-readable text (the usual
            // place a printed barcode shows it) — BarcodeGenerator only ever produces the bars
            // themselves, it has no text-drawing capability of its own (ZXing's PixelData writer is a
            // pure module-raster generator). Never let the strip eat the whole box on a very short
            // element.
            var textHeightDots = Math.Min(payload.TextSize * dpi / 25.4, bounds.Height * 0.4);
            textBounds = new XRect(bounds.Left, bounds.Bottom - textHeightDots, bounds.Width, textHeightDots);
            barsBounds = new XRect(bounds.Left, bounds.Top, bounds.Width, bounds.Height - textHeightDots);
        }

        var image = BarcodeGenerator.GenerateBarcode(payload.Data, payload.Symbology, (int)barsBounds.Width, (int)barsBounds.Height);
        var xImage = DrawPixelImage(gfx, barsBounds, image);

        if (textBounds is { } text && text.Height >= 1)
        {
            DrawBarcodeText(gfx, payload.Data, text);
        }

        return xImage;
    }

    private static void DrawBarcodeText(XGraphics gfx, string text, XRect bounds)
    {
        // bounds.Height is already expressed in the pre-scaled "dots" coordinate space PdfExporter's
        // global ScaleTransform expects font sizes in — see the identical convention in TextDrawing.cs.
        var font = new XFont("Arial", bounds.Height * 0.8, XFontStyleEx.Regular);
        var format = new XStringFormat { Alignment = XStringAlignment.Center, LineAlignment = XLineAlignment.Near };
        gfx.DrawString(text, font, XBrushes.Black, bounds, format);
    }

    public static XImage DrawQrCode(XGraphics gfx, XRect bounds, ResolvedQrCodePayload payload)
    {
        var size = (int)Math.Min(bounds.Width, bounds.Height);
        var image = BarcodeGenerator.GenerateQrCode(payload.Data, payload.ErrorCorrectionLevel, size);
        return DrawPixelImage(gfx, bounds, image);
    }

    private static XImage DrawPixelImage(XGraphics gfx, XRect bounds, BarcodeImage image)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using var bitmap = new SKBitmap(info);
        Marshal.Copy(image.Bgra32Pixels, 0, bitmap.GetPixels(), image.Bgra32Pixels.Length);

        using var data = bitmap.Encode(SKEncodedImageFormat.Png, quality: 100);
        var xImage = XImage.FromStream(new MemoryStream(data.ToArray()));
        gfx.DrawImage(xImage, bounds);
        return xImage;
    }
}
