namespace LabelSharpDesignerCore.Rendering.ArgoxPpla.Tests;

public class MonochromeBmpEncoderTests
{
    private static MonochromeBitmap TwoByTwoBitmap() => new()
    {
        WidthPx = 8,
        HeightPx = 2,
        Rows =
        [
            [0b1000_0000], // row 0: only the first pixel is "dark"
            [0b0000_0001], // row 1: only the last pixel is "dark"
        ],
    };

    [Fact]
    public void Encode_StartsWithTheBmpSignature()
    {
        var bytes = MonochromeBmpEncoder.Encode(TwoByTwoBitmap());

        Assert.Equal((byte)'B', bytes[0]);
        Assert.Equal((byte)'M', bytes[1]);
    }

    [Fact]
    public void Encode_AlwaysWritesAPositiveBiHeight()
    {
        // A negative biHeight is confirmed to hang real Argox hardware — this must never happen,
        // regardless of reverseRowOrder (which controls row order a different way; see the encoder's
        // own remarks).
        var positiveTrue = MonochromeBmpEncoder.Encode(TwoByTwoBitmap(), reverseRowOrder: true);
        var positiveFalse = MonochromeBmpEncoder.Encode(TwoByTwoBitmap(), reverseRowOrder: false);

        Assert.Equal(2, BitConverter.ToInt32(positiveTrue, 22));
        Assert.Equal(2, BitConverter.ToInt32(positiveFalse, 22));
    }

    [Fact]
    public void Encode_WritesA1BitPerPixelBiCount()
    {
        var bytes = MonochromeBmpEncoder.Encode(TwoByTwoBitmap());

        Assert.Equal(1, BitConverter.ToUInt16(bytes, 28));
    }

    [Fact]
    public void Encode_WritesAnInvertedBlackThenWhitePalette()
    {
        var bytes = MonochromeBmpEncoder.Encode(TwoByTwoBitmap());

        // Palette starts right after the 14-byte file header + 40-byte info header, at offset 54.
        Assert.Equal([0x00, 0x00, 0x00, 0x00], bytes[54..58]); // index 0 = black
        Assert.Equal([0xFF, 0xFF, 0xFF, 0x00], bytes[58..62]); // index 1 = white
    }

    [Fact]
    public void Encode_InvertsPixelBitsRelativeToTheDarkOneConvention()
    {
        var pixelDataOffset = 14 + 40 + 8;
        var rowBytesPadded = 4; // ((8 + 31) / 32) * 4

        var unreversed = MonochromeBmpEncoder.Encode(TwoByTwoBitmap(), reverseRowOrder: false);

        // Row 0 source byte 0b10000000 inverted -> 0b01111111 (0x7F).
        Assert.Equal(0x7F, unreversed[pixelDataOffset]);
        // Row 1 source byte 0b00000001 inverted -> 0b11111110 (0xFE).
        Assert.Equal(0xFE, unreversed[pixelDataOffset + rowBytesPadded]);
    }

    [Fact]
    public void Encode_ReverseRowOrder_WritesTheLastScanlineFirst()
    {
        var pixelDataOffset = 14 + 40 + 8;
        var rowBytesPadded = 4;

        var reversed = MonochromeBmpEncoder.Encode(TwoByTwoBitmap(), reverseRowOrder: true);

        // With reversal, row 1's (inverted) byte comes first, row 0's second — the opposite of the
        // unreversed case verified above.
        Assert.Equal(0xFE, reversed[pixelDataOffset]);
        Assert.Equal(0x7F, reversed[pixelDataOffset + rowBytesPadded]);
    }

    [Fact]
    public void Encode_PadsEachRowToAFourByteBoundary()
    {
        var bitmap = new MonochromeBitmap { WidthPx = 1, HeightPx = 1, Rows = [[0b1000_0000]] };

        var bytes = MonochromeBmpEncoder.Encode(bitmap);

        var pixelDataOffset = 14 + 40 + 8;
        var expectedFileSize = pixelDataOffset + 4; // 1 row, padded to 4 bytes even though 1px needs 1 bit
        Assert.Equal(expectedFileSize, bytes.Length);
    }
}
