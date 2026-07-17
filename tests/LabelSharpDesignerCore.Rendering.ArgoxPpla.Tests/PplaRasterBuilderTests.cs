using LabelSharpDesignerCore.Core.Document;
using LabelSharpDesignerCore.Core.Elements;
using LabelSharpDesignerCore.Core.Geometry;
using LabelSharpDesignerCore.Layout;

namespace LabelSharpDesignerCore.Rendering.ArgoxPpla.Tests;

public class PplaRasterBuilderTests
{
    private static string Decode(byte[] bytes) => new(Array.ConvertAll(bytes, b => (char)b));

    private static LabelDocument SampleWithQrAndImage() => new()
    {
        Name = "Raster",
        Page = new PageConfig { WidthMm = 40, HeightMm = 20, Dpi = 203 },
        Elements =
        [
            new QrCodeElement { Id = "q1", Position = new PointMm(2, 2), Size = new SizeMm(15, 15), Data = "https://example.com" },
            new TextElement { Id = "t1", Position = new PointMm(20, 2), Size = new SizeMm(18, 8), Content = "Ola" },
        ],
    };

    [Fact]
    public void Build_ProducesNonEmptyBytesForContentPplaCommandBuilderWouldSkip()
    {
        var document = new LayoutEngine().Resolve(SampleWithQrAndImage());

        var bytes = PplaRasterBuilder.Build(document);

        Assert.True(bytes.Length > 200, $"Expected a non-trivial raster job, got {bytes.Length} bytes.");
    }

    [Fact]
    public void Build_FramesTheImageWithClearMemoryAndImageDownloadCommands()
    {
        var document = new LayoutEngine().Resolve(SampleWithQrAndImage());

        var text = Decode(PplaRasterBuilder.Build(document));

        Assert.StartsWith("\x02qA\r", text); // clear memory
        Assert.Contains("\x02IDbLBL\r", text); // <STX>I + default bank 'D' + format 'b' + default name 'LBL'
        Assert.Contains("1Y11000" + "0000" + "0000" + "LBL\r", text); // image placement record at the label's own origin
        Assert.EndsWith("E\r", text);
    }

    [Fact]
    public void Build_UsesTheConfiguredImageNameAndMemoryBank()
    {
        var document = new LayoutEngine().Resolve(SampleWithQrAndImage());

        var text = Decode(PplaRasterBuilder.Build(document, new ArgoxRasterOptions { MemoryBank = "E", ImageName = "JOB1" }));

        Assert.Contains("\x02IEbJOB1\r", text);
        Assert.Contains("JOB1\r", text);
    }

    [Fact]
    public void Build_FullResolution_RequestsD11RegardlessOfDpi()
    {
        var document = new LayoutEngine().Resolve(new LabelDocument { Name = "A", Page = new PageConfig { WidthMm = 40, HeightMm = 20, Dpi = 203 } });

        var text = Decode(PplaRasterBuilder.Build(document, new ArgoxRasterOptions { FullResolution = true }));

        Assert.Contains("D11\r", text);
    }

    [Fact]
    public void Build_WithoutFullResolution_FallsBackToTheDpiBasedDotMultiplier()
    {
        var document = new LayoutEngine().Resolve(new LabelDocument { Name = "A", Page = new PageConfig { WidthMm = 40, HeightMm = 20, Dpi = 203 } });

        var text = Decode(PplaRasterBuilder.Build(document, new ArgoxRasterOptions { FullResolution = false }));

        Assert.Contains("D22\r", text);
    }
}
