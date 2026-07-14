using LabelSharpDesigner.Core.Layout;
using LabelSharpDesigner.Rendering.Abstractions;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace LabelSharpDesigner.Rendering.Pdf;

/// <summary>
/// Renders a laid-out <see cref="ResolvedDocument"/> to vectorial PDF bytes using PdfSharp —
/// real embedded fonts and vector shapes for text/lines/rectangles/ellipses (barcodes/QR codes are
/// raster-embedded, see <see cref="BarcodeDrawing"/>). Unlike <c>Rendering.Canvas</c>, this does not
/// share drawing code with the editor/PNG path (PdfSharp's <c>XGraphics</c> and SkiaSharp's
/// <c>SKCanvas</c> are unrelated APIs) — both dispatch over the same <see cref="ResolvedDocument"/>
/// shape via <see cref="IResolvedPayloadVisitor{TResult}"/> so they can't structurally diverge.
/// </summary>
public static class PdfExporter
{
    static PdfExporter()
    {
        // PdfSharp 6.x (the "Core build") no longer resolves system fonts automatically on any
        // platform, even Windows — this opt-in flag restores that behavior via PdfSharp's own
        // built-in Windows font resolver. Acceptable here because this app only ever runs on
        // Windows (WinForms + legacy ASP.NET Framework bridge); see
        // https://docs.pdfsharp.net/link/font-resolving.html.
        GlobalFontSettings.UseWindowsFontsUnderWindows = true;
    }

    public static byte[] Export(ResolvedDocument document) => ExportBatch([document]);

    /// <summary>Renders each <paramref name="documents"/> entry as its own page in one PDF, in order
    /// — used for a mail-merge/multi-column print job's rows (see <c>LayoutEngine.ResolveBatch</c>),
    /// where each row is a physically separate printed sheet. A single-element list is exactly
    /// <see cref="Export"/>'s old single-page behavior.</summary>
    public static byte[] ExportBatch(IReadOnlyList<ResolvedDocument> documents)
    {
        using var pdfDocument = new PdfDocument();

        // PdfSharp stamps its own Creator/CreationDate/etc. into PdfDocument.Info by default — none
        // of that is meaningful for a label meant to go straight to a printer, and it's not something
        // this app's users asked to have embedded, so blank it out. (Only what's necessary for print
        // output — the page geometry and drawn elements below — should end up in the file.)
        pdfDocument.Info.Title = string.Empty;
        pdfDocument.Info.Author = string.Empty;
        pdfDocument.Info.Subject = string.Empty;
        pdfDocument.Info.Keywords = string.Empty;
        pdfDocument.Info.Creator = string.Empty;

        var trackedImages = new List<XImage>();
        try
        {
            foreach (var document in documents)
            {
                var scale = 72.0 / document.Dpi;
                var page = pdfDocument.AddPage();
                page.Width = XUnit.FromPoint(document.WidthDots * scale);
                page.Height = XUnit.FromPoint(document.HeightDots * scale);

                using var gfx = XGraphics.FromPdfPage(page);
                // Every subsequent coordinate (bounds, stroke widths, font sizes-in-dots) is drawn
                // directly in document dots; this one global scale converts the whole page to points.
                gfx.ScaleTransform(scale);

                foreach (var element in document.Elements.OrderBy(e => e.ZIndex))
                {
                    DrawElement(gfx, element, document.Dpi, trackedImages);
                }
            }

            using var stream = new MemoryStream();
            pdfDocument.Save(stream, false);
            return stream.ToArray();
        }
        finally
        {
            foreach (var image in trackedImages)
            {
                image.Dispose();
            }
        }
    }

    private static void DrawElement(XGraphics gfx, ResolvedElement element, int dpi, List<XImage> trackedImages)
    {
        var bounds = new XRect(element.XDots, element.YDots, element.WidthDots, element.HeightDots);
        var center = new XPoint(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);

        var state = gfx.Save();

        if (element.RotationDegrees != 0)
        {
            gfx.RotateAtTransform(element.RotationDegrees, center);
        }

        var scaleX = element.Transform.FlipH ? -1.0 : 1.0;
        var scaleY = element.Transform.FlipV ? -1.0 : 1.0;
        if (scaleX != 1.0 || scaleY != 1.0)
        {
            gfx.ScaleAtTransform(scaleX, scaleY, center);
        }

        if (element.Transform.SkewX != 0 || element.Transform.SkewY != 0)
        {
            gfx.SkewAtTransform(element.Transform.SkewX, element.Transform.SkewY, center);
        }

        var image = element.Payload.Accept(new PayloadDrawingVisitor(gfx, bounds, element.Opacity, dpi));
        if (image is not null)
        {
            trackedImages.Add(image);
        }

        gfx.Restore(state);
    }

    private sealed class PayloadDrawingVisitor(XGraphics gfx, XRect bounds, double opacity, int dpi) : IResolvedPayloadVisitor<XImage?>
    {
        public XImage? VisitText(ResolvedTextPayload payload)
        {
            TextDrawing.Draw(gfx, bounds, payload.Text, payload.Style, opacity, dpi);
            return null;
        }

        public XImage? VisitBarcode(ResolvedBarcodePayload payload) => BarcodeDrawing.DrawBarcode(gfx, bounds, payload, dpi);

        public XImage? VisitQrCode(ResolvedQrCodePayload payload) => BarcodeDrawing.DrawQrCode(gfx, bounds, payload);

        public XImage? VisitImage(ResolvedImagePayload payload) => ImageDrawing.Draw(gfx, bounds, payload.Source, payload.Fit);

        public XImage? VisitShape(ResolvedShapePayload payload)
        {
            ShapeDrawing.Draw(gfx, bounds, payload, opacity);
            return null;
        }

        public XImage? VisitTable(ResolvedTablePayload payload)
        {
            TableDrawing.Draw(gfx, bounds, payload, dpi);
            return null;
        }
    }
}
