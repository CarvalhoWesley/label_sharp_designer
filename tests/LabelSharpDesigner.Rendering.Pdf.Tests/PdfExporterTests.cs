using LabelSharpDesigner.Core.Document;
using LabelSharpDesigner.Core.Elements;
using LabelSharpDesigner.Core.Geometry;
using LabelSharpDesigner.Core.Layout;
using LabelSharpDesigner.Core.Styles;
using LabelSharpDesigner.Layout;
using PdfSharp.Pdf.IO;

namespace LabelSharpDesigner.Rendering.Pdf.Tests;

public class PdfExporterTests
{
    private static LabelDocument SampleDocument() => new()
    {
        Name = "Sample",
        Page = new PageConfig { WidthMm = 40, HeightMm = 20, Dpi = 203 },
        Elements =
        [
            new RectangleElement
            {
                Id = "r1",
                Position = new PointMm(2, 2),
                Size = new SizeMm(36, 16),
                Style = new ShapeStyleSpec { BorderColor = ArgbColor.Black, BorderWidthMm = 0.5, FillColor = ArgbColor.White },
            },
            new TextElement
            {
                Id = "t1",
                Position = new PointMm(4, 4),
                Size = new SizeMm(30, 6),
                Content = "Hello",
                Style = TextStyleSpec.Default,
            },
        ],
    };

    private static ResolvedDocument Resolve(LabelDocument? document = null) => new LayoutEngine().Resolve(document ?? SampleDocument());

    [Fact]
    public void Export_ProducesBytesStartingWithThePdfSignature()
    {
        var bytes = PdfExporter.Export(Resolve());

        var signature = System.Text.Encoding.ASCII.GetString(bytes, 0, 5);
        Assert.Equal("%PDF-", signature);
    }

    [Fact]
    public void Export_ProducesASinglePageSizedFromDotsAndDpi()
    {
        var resolved = Resolve();

        var bytes = PdfExporter.Export(resolved);

        using var stream = new MemoryStream(bytes);
        using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);

        Assert.Equal(1, document.PageCount);
        var page = document.Pages[0];
        var expectedWidthPt = resolved.WidthDots * 72.0 / resolved.Dpi;
        var expectedHeightPt = resolved.HeightDots * 72.0 / resolved.Dpi;
        Assert.Equal(expectedWidthPt, page.Width.Point, precision: 1);
        Assert.Equal(expectedHeightPt, page.Height.Point, precision: 1);
    }

    [Fact]
    public void Export_ProducesNonTrivialContent()
    {
        var bytes = PdfExporter.Export(Resolve());

        // A blank/near-empty page would be a few hundred bytes; a page with a filled rectangle and
        // real text content is comfortably larger even after Flate compression of the content stream.
        Assert.True(bytes.Length > 1000, $"Expected a non-trivial PDF, got {bytes.Length} bytes.");
    }

    [Fact]
    public void Export_EmbedsBarcodesAndQrCodesAsImageXObjects()
    {
        var document = new LabelDocument
        {
            Name = "Barcode",
            Page = new PageConfig { WidthMm = 60, HeightMm = 30, Dpi = 203 },
            Elements =
            [
                new BarcodeElement
                {
                    Id = "b1",
                    Position = new PointMm(2, 2),
                    Size = new SizeMm(40, 15),
                    Data = "123456789012",
                    Symbology = BarcodeSymbology.Code128,
                },
                new QrCodeElement
                {
                    Id = "q1",
                    Position = new PointMm(2, 18),
                    Size = new SizeMm(10, 10),
                    Data = "https://example.com",
                },
            ],
        };

        var bytes = PdfExporter.Export(Resolve(document));

        var text = System.Text.Encoding.Latin1.GetString(bytes);
        Assert.Contains("/Subtype/Image", text);
    }

    [Fact]
    public void ExportBatch_ProducesOnePagePerRowInOrder()
    {
        var document = SampleDocument();
        var rows = new LayoutEngine().ResolveBatch(
            document,
            [new Dictionary<string, object?>(), new Dictionary<string, object?>(), new Dictionary<string, object?>()]);

        var bytes = PdfExporter.ExportBatch(rows);

        using var stream = new MemoryStream(bytes);
        using var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Import);

        // Columns defaults to 1, so 3 records == 3 physical rows == 3 pages, each sized like the
        // single-row Export() case above.
        Assert.Equal(3, pdf.PageCount);
        var expectedWidthPt = rows[0].WidthDots * 72.0 / rows[0].Dpi;
        Assert.Equal(expectedWidthPt, pdf.Pages[0].Width.Point, precision: 1);
        Assert.Equal(expectedWidthPt, pdf.Pages[2].Width.Point, precision: 1);
    }

    [Fact]
    public void Export_WithRotatedAndFlippedElement_DoesNotThrow()
    {
        var document = new LabelDocument
        {
            Name = "Transformed",
            Page = new PageConfig { WidthMm = 40, HeightMm = 40, Dpi = 203 },
            Elements =
            [
                new EllipseElement
                {
                    Id = "e1",
                    Position = new PointMm(5, 5),
                    Size = new SizeMm(20, 10),
                    RotationDegrees = 30,
                    Transform = new ElementTransform { FlipH = true, SkewX = 5 },
                    Style = new ShapeStyleSpec { BorderColor = ArgbColor.Black, BorderWidthMm = 0.5 },
                    Opacity = 0.5,
                },
            ],
        };

        var bytes = PdfExporter.Export(Resolve(document));

        Assert.True(bytes.Length > 0);
    }
}
