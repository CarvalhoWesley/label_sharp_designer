using LabelSharpDesignerCore.Core.Document;
using LabelSharpDesignerCore.Core.Elements;
using LabelSharpDesignerCore.Core.Geometry;
using LabelSharpDesignerCore.Core.Styles;
using SkiaSharp;

namespace LabelSharpDesignerCore.Rendering.Png.Tests;

public class PngExporterTests
{
    private static LabelDocument SampleDocument() => new()
    {
        Name = "Sample",
        Page = new PageConfig { WidthMm = 40, HeightMm = 20, Dpi = 203 },
        Elements = new LabelElement[]
        {
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
        },
    };

    private static Core.Layout.ResolvedDocument Resolve() => new Layout.LayoutEngine().Resolve(SampleDocument());

    [Theory]
    [InlineData(PngScale.X1, 1)]
    [InlineData(PngScale.X2, 2)]
    [InlineData(PngScale.X3, 3)]
    public void Export_ProducesPngAtTheRequestedScale(PngScale scale, int factor)
    {
        var resolved = Resolve();

        var bytes = PngExporter.Export(resolved, scale);

        using var image = SKBitmap.Decode(bytes);
        Assert.Equal(resolved.WidthDots * factor, image.Width);
        Assert.Equal(resolved.HeightDots * factor, image.Height);
    }

    [Fact]
    public void Export_ProducesAValidPngSignature()
    {
        var bytes = PngExporter.Export(Resolve());

        byte[] pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        Assert.Equal(pngSignature, bytes.Take(8));
    }

    [Theory]
    [InlineData(240)]
    [InlineData(80)]
    public void ExportScaled_ProducesAPngWithTheRequestedWidth(int targetWidthPx)
    {
        var resolved = Resolve();

        var bytes = PngExporter.ExportScaled(resolved, targetWidthPx);

        using var image = SKBitmap.Decode(bytes);
        Assert.Equal(targetWidthPx, image.Width);
        var expectedHeight = (int)Math.Round(resolved.HeightDots * ((float)targetWidthPx / resolved.WidthDots));
        Assert.Equal(expectedHeight, image.Height);
    }

    [Fact]
    public void Export_DrawsNonBlankContent()
    {
        var bytes = PngExporter.Export(Resolve());

        using var bitmap = SKBitmap.Decode(bytes);
        var sawNonWhitePixel = false;
        for (var y = 0; y < bitmap.Height && !sawNonWhitePixel; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y) != SKColors.White)
                {
                    sawNonWhitePixel = true;
                    break;
                }
            }
        }

        Assert.True(sawNonWhitePixel, "Expected the rendered label to contain non-white pixels (border/text).");
    }

    private static LabelDocument BarcodeDocument(bool showText) => new()
    {
        Name = "Barcode",
        Page = new PageConfig { WidthMm = 50, HeightMm = 25, Dpi = 203 },
        Elements =
        [
            new BarcodeElement
            {
                Id = "b1",
                Position = new PointMm(2, 2),
                Size = new SizeMm(46, 20),
                Data = "123456789012",
                Symbology = BarcodeSymbology.Code128,
                ShowText = showText,
                TextSize = 4,
            },
        ],
    };

    /// <summary>ZXing renders bars as solid black/white with no anti-aliasing, so a gray
    /// (neither-pure-black-nor-white) pixel in the reserved text strip is the signature of drawn text
    /// glyphs rather than bars extending into that area.</summary>
    private static bool HasAntiAliasedPixelInBottomStrip(SKBitmap bitmap, Core.Layout.ResolvedDocument resolved, double elementBottomMm, double stripHeightMm)
    {
        var stripBottomY = (int)(elementBottomMm * resolved.Dpi / 25.4);
        var stripTopY = stripBottomY - (int)(stripHeightMm * resolved.Dpi / 25.4);

        for (var y = Math.Max(0, stripTopY); y < Math.Min(bitmap.Height, stripBottomY); y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.Alpha > 0 && pixel != SKColors.White && pixel != SKColors.Black)
                {
                    return true;
                }
            }
        }

        return false;
    }

    [Fact]
    public void Export_BarcodeWithShowText_DrawsTextBelowTheBars()
    {
        var resolved = new Layout.LayoutEngine().Resolve(BarcodeDocument(showText: true));

        using var bitmap = SKBitmap.Decode(PngExporter.Export(resolved));

        Assert.True(
            HasAntiAliasedPixelInBottomStrip(bitmap, resolved, elementBottomMm: 22, stripHeightMm: 4),
            "Expected anti-aliased barcode text pixels in the reserved text strip.");
    }

    [Fact]
    public void Export_BarcodeWithoutShowText_DrawsNoTextBelowTheBars()
    {
        var resolved = new Layout.LayoutEngine().Resolve(BarcodeDocument(showText: false));

        using var bitmap = SKBitmap.Decode(PngExporter.Export(resolved));

        Assert.False(
            HasAntiAliasedPixelInBottomStrip(bitmap, resolved, elementBottomMm: 22, stripHeightMm: 4),
            "Expected no text (only solid black/white bars) when ShowText is false.");
    }

    [Fact]
    public void Export_LongTextInNarrowElement_WrapsToMultipleLines()
    {
        var page = new PageConfig { WidthMm = 40, HeightMm = 40, Dpi = 203 };
        var style = TextStyleSpec.Default with { FontSizePt = 10 };

        LabelDocument DocumentWith(string content) => new()
        {
            Name = "Wrap",
            Page = page,
            Elements =
            [
                new TextElement
                {
                    Id = "t1",
                    Position = new PointMm(2, 2),
                    Size = new SizeMm(20, 36),
                    Content = content,
                    Style = style,
                },
            ],
        };

        var shortBitmapBytes = PngExporter.Export(new Layout.LayoutEngine().Resolve(DocumentWith("Hi")));
        var longBitmapBytes = PngExporter.Export(new Layout.LayoutEngine().Resolve(DocumentWith(
            "This is a much longer piece of text that cannot possibly fit on a single line within this narrow element")));

        using var shortBitmap = SKBitmap.Decode(shortBitmapBytes);
        using var longBitmap = SKBitmap.Decode(longBitmapBytes);

        var shortLastInkedRow = LastNonWhiteRow(shortBitmap);
        var longLastInkedRow = LastNonWhiteRow(longBitmap);

        Assert.True(
            longLastInkedRow > shortLastInkedRow + 5,
            $"Expected wrapped multi-line text to extend further down than single-line text (short={shortLastInkedRow}, long={longLastInkedRow}).");
    }

    /// <summary>VariableElement/DateElement/TimeElement all resolve into the exact same
    /// <c>ResolvedTextPayload</c> as TextElement (see <c>ElementResolvingVisitor</c>) and are drawn by
    /// the same <c>TextDrawing.Draw</c> call — this and <see cref="Export_LongDateFormatLiteral_WrapsToMultipleLines"/>
    /// confirm wrapping already applies to them too, the same way <see cref="Export_LongTextInNarrowElement_WrapsToMultipleLines"/>
    /// confirms it for plain text.</summary>
    [Fact]
    public void Export_LongVariableValue_WrapsToMultipleLines()
    {
        var page = new PageConfig { WidthMm = 40, HeightMm = 40, Dpi = 203 };
        var style = TextStyleSpec.Default with { FontSizePt = 10 };

        LabelDocument DocumentWith(string expression) => new()
        {
            Name = "Wrap",
            Page = page,
            Elements =
            [
                new VariableElement
                {
                    Id = "v1",
                    Position = new PointMm(2, 2),
                    Size = new SizeMm(20, 36),
                    Expression = expression,
                    Style = style,
                },
            ],
        };

        var shortBitmapBytes = PngExporter.Export(new Layout.LayoutEngine().Resolve(DocumentWith("'Hi'")));
        var longBitmapBytes = PngExporter.Export(new Layout.LayoutEngine().Resolve(DocumentWith(
            "'This is a much longer piece of text that cannot possibly fit on a single line within this narrow element'")));

        using var shortBitmap = SKBitmap.Decode(shortBitmapBytes);
        using var longBitmap = SKBitmap.Decode(longBitmapBytes);

        Assert.True(
            LastNonWhiteRow(longBitmap) > LastNonWhiteRow(shortBitmap) + 5,
            "Expected a long evaluated variable value to wrap across multiple lines, same as plain text.");
    }

    [Fact]
    public void Export_LongDateFormatLiteral_WrapsToMultipleLines()
    {
        var page = new PageConfig { WidthMm = 40, HeightMm = 40, Dpi = 203 };
        var style = TextStyleSpec.Default with { FontSizePt = 10 };

        LabelDocument DocumentWith(string format) => new()
        {
            Name = "Wrap",
            Page = page,
            Elements =
            [
                new DateElement
                {
                    Id = "d1",
                    Position = new PointMm(2, 2),
                    Size = new SizeMm(20, 36),
                    Format = format,
                    Style = style,
                },
            ],
        };

        // A double-quoted section in a .NET custom date/time format string is emitted verbatim,
        // regardless of the actual date value — used here to get an arbitrarily long literal string
        // out of DateElement without depending on today's date.
        var shortBitmapBytes = PngExporter.Export(new Layout.LayoutEngine().Resolve(DocumentWith("\"Hi\"")));
        var longBitmapBytes = PngExporter.Export(new Layout.LayoutEngine().Resolve(DocumentWith(
            "\"This is a much longer piece of text that cannot possibly fit on a single line within this narrow element\"")));

        using var shortBitmap = SKBitmap.Decode(shortBitmapBytes);
        using var longBitmap = SKBitmap.Decode(longBitmapBytes);

        Assert.True(
            LastNonWhiteRow(longBitmap) > LastNonWhiteRow(shortBitmap) + 5,
            "Expected a long date/time literal to wrap across multiple lines, same as plain text.");
    }

    private static int LastNonWhiteRow(SKBitmap bitmap)
    {
        for (var y = bitmap.Height - 1; y >= 0; y--)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y) != SKColors.White)
                {
                    return y;
                }
            }
        }

        return -1;
    }
}
