namespace LabelSharpDesignerCore.Rendering.ArgoxPpla.Tests;

public class MonochromeBitmapTests
{
    private static byte[] SolidRgba(int widthPx, int heightPx, byte r, byte g, byte b)
    {
        var rgba = new byte[widthPx * heightPx * 4];
        for (var i = 0; i < rgba.Length; i += 4)
        {
            rgba[i] = r;
            rgba[i + 1] = g;
            rgba[i + 2] = b;
            rgba[i + 3] = 255;
        }

        return rgba;
    }

    [Fact]
    public void FromRgba_BlackPixelsBecomeDarkBits()
    {
        var rgba = SolidRgba(8, 1, 0, 0, 0);

        var bitmap = MonochromeBitmap.FromRgba(rgba, widthPx: 8, heightPx: 1);

        Assert.Equal(0b1111_1111, bitmap.Rows[0][0]);
    }

    [Fact]
    public void FromRgba_WhitePixelsBecomeLightBits()
    {
        var rgba = SolidRgba(8, 1, 255, 255, 255);

        var bitmap = MonochromeBitmap.FromRgba(rgba, widthPx: 8, heightPx: 1);

        Assert.Equal(0, bitmap.Rows[0][0]);
    }

    [Fact]
    public void FromRgba_MirrorHorizontal_ReversesTheColumnOrder()
    {
        // Only the leftmost pixel is dark.
        var rgba = new byte[8 * 4];
        rgba[0] = 0; rgba[1] = 0; rgba[2] = 0; rgba[3] = 255;
        for (var i = 4; i < rgba.Length; i += 4)
        {
            rgba[i] = 255; rgba[i + 1] = 255; rgba[i + 2] = 255; rgba[i + 3] = 255;
        }

        var unmirrored = MonochromeBitmap.FromRgba(rgba, widthPx: 8, heightPx: 1, mirrorHorizontal: false);
        var mirrored = MonochromeBitmap.FromRgba(rgba, widthPx: 8, heightPx: 1, mirrorHorizontal: true);

        Assert.Equal(0b1000_0000, unmirrored.Rows[0][0]); // dark pixel stays leftmost (MSB)
        Assert.Equal(0b0000_0001, mirrored.Rows[0][0]); // dark pixel moves to rightmost (LSB)
    }

    [Fact]
    public void FromRgba_ThresholdControlsWhatCountsAsDark()
    {
        var rgba = SolidRgba(8, 1, 100, 100, 100); // mid-gray, luminance ~100

        var darkAtLowThreshold = MonochromeBitmap.FromRgba(rgba, widthPx: 8, heightPx: 1, threshold: 50);
        var darkAtHighThreshold = MonochromeBitmap.FromRgba(rgba, widthPx: 8, heightPx: 1, threshold: 200);

        Assert.Equal(0, darkAtLowThreshold.Rows[0][0]); // 100 is not below 50 -> not dark
        Assert.Equal(0b1111_1111, darkAtHighThreshold.Rows[0][0]); // 100 is below 200 -> dark
    }
}
