using LabelSharpDesignerCore.Core.Document;
using LabelSharpDesignerCore.Core.Elements;
using LabelSharpDesignerCore.Core.Geometry;
using LabelSharpDesignerCore.Core.Styles;
using LabelSharpDesignerCore.Layout;

namespace LabelSharpDesignerCore.Rendering.ArgoxPpla.Tests;

public class PplaCommandBuilderTests
{
    private static string Decode(byte[] bytes) => new(Array.ConvertAll(bytes, b => (char)b));

    [Fact]
    public void Build_StartsWithClearMemoryThenEntersLabelFormattingMode()
    {
        var document = new LayoutEngine().Resolve(new LabelDocument
        {
            Name = "Empty",
            Page = new PageConfig { WidthMm = 40, HeightMm = 20, Dpi = 203 },
        });

        var text = Decode(PplaCommandBuilder.Build(document));

        Assert.StartsWith("\x02qA\r", text);
        Assert.Contains("\x02L\r", text); // enter label formatting mode
        Assert.EndsWith("E\r", text); // end job / print
    }

    [Fact]
    public void Build_UsesD22DotSizeAt203DpiAndD11Above()
    {
        var doc203 = new LayoutEngine().Resolve(new LabelDocument { Name = "A", Page = new PageConfig { WidthMm = 40, HeightMm = 20, Dpi = 203 } });
        var doc300 = new LayoutEngine().Resolve(new LabelDocument { Name = "B", Page = new PageConfig { WidthMm = 40, HeightMm = 20, Dpi = 300 } });

        Assert.Contains("D22\r", Decode(PplaCommandBuilder.Build(doc203)));
        Assert.Contains("D11\r", Decode(PplaCommandBuilder.Build(doc300)));
    }

    [Fact]
    public void Build_ClampsDarknessAndCopiesIntoValidRanges()
    {
        var document = new LayoutEngine().Resolve(new LabelDocument { Name = "A", Page = new PageConfig { WidthMm = 40, HeightMm = 20, Dpi = 203 } });

        var text = Decode(PplaCommandBuilder.Build(document, new ArgoxRendererOptions { Darkness = 999, Copies = 0 }));

        Assert.Contains("H20\r", text); // darkness clamped to max 20
        Assert.Contains("Q0001\r", text); // copies clamped to min 1
    }

    [Fact]
    public void Build_WithPplbDialect_Throws()
    {
        var document = new LayoutEngine().Resolve(new LabelDocument { Name = "A", Page = new PageConfig { WidthMm = 40, HeightMm = 20, Dpi = 203 } });

        Assert.Throws<NotSupportedException>(() => PplaCommandBuilder.Build(document, new ArgoxRendererOptions { Dialect = ArgoxDialect.Pplb }));
    }

    [Fact]
    public void Build_EncodesATextElementWithFontTypeNineAndItsContent()
    {
        var document = new LayoutEngine().Resolve(new LabelDocument
        {
            Name = "Text",
            Page = new PageConfig { WidthMm = 40, HeightMm = 20, Dpi = 203 },
            Elements = [new TextElement { Id = "t1", Position = new PointMm(2, 2), Size = new SizeMm(30, 8), Content = "OLA" }],
        });

        var text = Decode(PplaCommandBuilder.Build(document));

        // Orientation(1) + font type(9) + hScale(1) + vScale(1) + 3-digit size code, then the text.
        Assert.Matches(@"19\d{5}\d{4}\d{4}OLA\r", text);
    }

    [Fact]
    public void Build_EncodesABarcodeElementWithItsTypeLetterAndData()
    {
        var document = new LayoutEngine().Resolve(new LabelDocument
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
                    ShowText = true,
                },
            ],
        });

        var text = Decode(PplaCommandBuilder.Build(document));

        Assert.Contains("123456789012\r", text);
        Assert.Matches(@"1E..\d{3}\d{4}\d{4}123456789012\r", text);
    }

    [Fact]
    public void Build_LowercasesTheBarcodeTypeLetterWhenTextIsHidden()
    {
        var document = new LayoutEngine().Resolve(new LabelDocument
        {
            Name = "Barcode",
            Page = new PageConfig { WidthMm = 60, HeightMm = 30, Dpi = 203 },
            Elements = [new BarcodeElement { Id = "b1", Position = new PointMm(2, 2), Size = new SizeMm(40, 15), Data = "123", Symbology = BarcodeSymbology.Code39, ShowText = false }],
        });

        var text = Decode(PplaCommandBuilder.Build(document));

        Assert.Contains("1a", text); // orientation 1 + lowercase 'a' (Code39, no human-readable text)
    }

    [Fact]
    public void Build_EncodesALineAsAnAxisAlignedLLine()
    {
        var document = new LayoutEngine().Resolve(new LabelDocument
        {
            Name = "Line",
            Page = new PageConfig { WidthMm = 40, HeightMm = 20, Dpi = 203 },
            Elements = [new LineElement { Id = "l1", Position = new PointMm(2, 10), Size = new SizeMm(30, 0) }],
        });

        var text = Decode(PplaCommandBuilder.Build(document));

        Assert.Matches(@"1X11000\d{4}\d{4}l\d{4}\d{4}\r", text);
    }

    [Fact]
    public void Build_EncodesARectangleAsAnOutlineBox()
    {
        var document = new LayoutEngine().Resolve(new LabelDocument
        {
            Name = "Rectangle",
            Page = new PageConfig { WidthMm = 40, HeightMm = 20, Dpi = 203 },
            Elements = [new RectangleElement { Id = "r1", Position = new PointMm(2, 2), Size = new SizeMm(30, 10), Style = new ShapeStyleSpec { BorderWidthMm = 0.5 } }],
        });

        var text = Decode(PplaCommandBuilder.Build(document));

        Assert.Matches(@"1X11000\d{4}\d{4}b\d{4}\d{4}\d{4}\d{4}\r", text);
    }

    [Fact]
    public void Build_SkipsQrCodeImageAndTableElementsRatherThanGuessing()
    {
        var document = new LayoutEngine().Resolve(new LabelDocument
        {
            Name = "Unsupported",
            Page = new PageConfig { WidthMm = 60, HeightMm = 60, Dpi = 203 },
            Elements =
            [
                new QrCodeElement { Id = "q1", Position = new PointMm(2, 2), Size = new SizeMm(15, 15), Data = "https://example.com" },
                new ImageElement { Id = "i1", Position = new PointMm(20, 2), Size = new SizeMm(15, 15), Source = "missing.png" },
            ],
        });

        var withElements = Decode(PplaCommandBuilder.Build(document));
        var withoutElements = Decode(PplaCommandBuilder.Build(new LayoutEngine().Resolve(new LabelDocument
        {
            Name = "Empty",
            Page = new PageConfig { WidthMm = 60, HeightMm = 60, Dpi = 203 },
        })));

        // Header/footer framing is identical whether or not the (skipped) QR/image elements are present.
        Assert.Equal(withoutElements, withElements);
    }
}
