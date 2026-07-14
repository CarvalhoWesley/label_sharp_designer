using LabelSharpDesigner.Core.Elements;
using LabelSharpDesigner.Core.Layout;
using LabelSharpDesigner.Core.Styles;
using LabelSharpDesigner.Rendering.Abstractions;
using SkiaSharp;

namespace LabelSharpDesigner.Rendering.Canvas;

/// <summary>
/// The single "real" label renderer: draws a laid-out <see cref="ResolvedDocument"/> onto an
/// <see cref="SKCanvas"/>. Both live preview (editor/export/print dialogs) and PNG export call
/// through here so the two can never visually diverge.
/// </summary>
public static class LabelCanvasRenderer
{
    public static void Render(SKCanvas canvas, ResolvedDocument document, SKColor? background = null)
    {
        // A rect fill (not canvas.Clear) — Clear always wipes the entire physical surface regardless
        // of the current transform/clip, which is correct when the surface is exactly page-sized (PNG
        // export, PPLA raster) but wrong for a preview control whose surface is larger than the page:
        // there, Clear would blow away the caller's own background (drawn to show the page's actual
        // dimensions against its surroundings) with solid white.
        using var backgroundPaint = new SKPaint { Color = background ?? SKColors.White, Style = SKPaintStyle.Fill };
        canvas.DrawRect(SKRect.Create(0, 0, document.WidthDots, document.HeightDots), backgroundPaint);

        foreach (var element in document.Elements)
        {
            DrawElement(canvas, element, document.Dpi);
        }
    }

    private static void DrawElement(SKCanvas canvas, ResolvedElement element, int dpi)
    {
        canvas.Save();

        var bounds = new SKRect(element.XDots, element.YDots, element.XDots + element.WidthDots, element.YDots + element.HeightDots);
        ApplyElementTransform(canvas, element, bounds);

        var clampedOpacity = element.Opacity < 0 ? 0 : element.Opacity > 1 ? 1 : element.Opacity;
        using (var layerPaint = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(clampedOpacity * 255)) })
        {
            canvas.SaveLayer(layerPaint);
            element.Payload.Accept(new PayloadDrawingVisitor(canvas, bounds, dpi));
            canvas.Restore();
        }

        canvas.Restore();
    }

    private static void ApplyElementTransform(SKCanvas canvas, ResolvedElement element, SKRect bounds)
    {
        var pivotX = bounds.MidX;
        var pivotY = bounds.MidY;

        canvas.Translate(pivotX, pivotY);

        if (element.RotationDegrees != 0)
        {
            canvas.RotateDegrees((float)element.RotationDegrees);
        }

        var scaleX = element.Transform.FlipH ? -1f : 1f;
        var scaleY = element.Transform.FlipV ? -1f : 1f;
        if (scaleX != 1f || scaleY != 1f)
        {
            canvas.Scale(scaleX, scaleY);
        }

        if (element.Transform.SkewX != 0 || element.Transform.SkewY != 0)
        {
            canvas.Skew(
                (float)Math.Tan(element.Transform.SkewX * Math.PI / 180),
                (float)Math.Tan(element.Transform.SkewY * Math.PI / 180));
        }

        canvas.Translate(-pivotX, -pivotY);
    }

    private sealed class PayloadDrawingVisitor : IResolvedPayloadVisitor<object?>
    {
        private readonly SKCanvas _canvas;
        private readonly SKRect _bounds;
        private readonly int _dpi;

        public PayloadDrawingVisitor(SKCanvas canvas, SKRect bounds, int dpi)
        {
            _canvas = canvas;
            _bounds = bounds;
            _dpi = dpi;
        }

        public object? VisitText(ResolvedTextPayload payload)
        {
            TextDrawing.Draw(_canvas, _bounds, payload.Text, payload.Style, _dpi);
            return null;
        }

        public object? VisitBarcode(ResolvedBarcodePayload payload)
        {
            var barsBounds = _bounds;
            SKRect? textBounds = null;

            if (payload.ShowText)
            {
                // Reserve a strip at the bottom of the element for the human-readable text (the usual
                // place a printed barcode shows it) — BarcodeGenerator only ever produces the bars
                // themselves, it has no text-drawing capability of its own (ZXing's PixelData writer
                // is a pure module-raster generator). Never let the strip eat the whole box on a very
                // short element.
                var textHeightDots = Math.Min((float)(payload.TextSize * _dpi / 25.4), _bounds.Height * 0.4f);
                textBounds = new SKRect(_bounds.Left, _bounds.Bottom - textHeightDots, _bounds.Right, _bounds.Bottom);
                barsBounds = new SKRect(_bounds.Left, _bounds.Top, _bounds.Right, _bounds.Bottom - textHeightDots);
            }

            var image = Barcode.BarcodeGenerator.GenerateBarcode(
                payload.Data, payload.Symbology, (int)barsBounds.Width, (int)barsBounds.Height);
            DrawPixelImage(_canvas, image, barsBounds);

            if (textBounds is { } bounds && bounds.Height >= 1)
            {
                DrawBarcodeText(payload.Data, bounds);
            }

            return null;
        }

        private void DrawBarcodeText(string text, SKRect bounds)
        {
            using var font = new SKFont(SKTypeface.Default, bounds.Height * 0.8f);
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };

            var textWidth = font.MeasureText(text);
            var x = bounds.MidX - textWidth / 2;
            var y = bounds.Bottom - font.Metrics.Descent;

            _canvas.Save();
            _canvas.ClipRect(bounds);
            _canvas.DrawText(text, x, y, SKTextAlign.Left, font, paint);
            _canvas.Restore();
        }

        public object? VisitQrCode(ResolvedQrCodePayload payload)
        {
            var size = (int)Math.Min(_bounds.Width, _bounds.Height);
            var image = Barcode.BarcodeGenerator.GenerateQrCode(payload.Data, payload.ErrorCorrectionLevel, size);
            DrawPixelImage(_canvas, image, _bounds);
            return null;
        }

        public object? VisitImage(ResolvedImagePayload payload)
        {
            ImageDrawing.Draw(_canvas, _bounds, payload.Source, payload.Fit);
            return null;
        }

        public object? VisitShape(ResolvedShapePayload payload)
        {
            ShapeDrawing.Draw(_canvas, _bounds, payload);
            return null;
        }

        public object? VisitTable(ResolvedTablePayload payload)
        {
            TableDrawing.Draw(_canvas, _bounds, payload, _dpi);
            return null;
        }

        private static void DrawPixelImage(SKCanvas canvas, Barcode.BarcodeImage image, SKRect bounds)
        {
            var info = new SKImageInfo(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
            using var bitmap = new SKBitmap(info);
            System.Runtime.InteropServices.Marshal.Copy(image.Bgra32Pixels, 0, bitmap.GetPixels(), image.Bgra32Pixels.Length);

            canvas.DrawBitmap(bitmap, bounds, SKSamplingOptions.Default);
        }
    }
}
