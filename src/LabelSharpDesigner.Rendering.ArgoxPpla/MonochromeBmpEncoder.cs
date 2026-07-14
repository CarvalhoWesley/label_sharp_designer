namespace LabelSharpDesigner.Rendering.ArgoxPpla;

/// <summary>
/// Encodes a <see cref="MonochromeBitmap"/> as a standard, minimal 1-bit-per-pixel Windows BMP file
/// (<c>BITMAPFILEHEADER</c> + <c>BITMAPINFOHEADER</c> + a 2-color palette + row-padded pixel data) —
/// the file format the Datamax Class Series 2 Programmer's Manual documents for PPLA's
/// <c>&lt;STX&gt;I</c> image-download command's <c>b</c>/<c>B</c> format designators.
///
/// <b><c>biHeight</c> is always written positive.</b> A negative <c>biHeight</c> (the spec-standard
/// way to mark a BMP top-down, paired with the manual's <c>B</c>/"flipped" designator) is confirmed
/// on real Argox hardware to hang the printer outright — it stops responding and needs a power
/// cycle. This printer's BMP parser apparently can't handle it at all, so this never emits one and
/// callers should never send the <c>B</c> designator either; only <c>b</c>.
///
/// <paramref name="reverseRowOrder"/> (see <see cref="Encode"/>) instead controls row order
/// directly, independent of any header flag: <see langword="true"/> writes a spec-standard bottom-up
/// arrangement (last scanline written first); <see langword="false"/> writes rows in their own
/// top-down order, unreversed. Which one a given printer's raster scan direction actually wants
/// still needs a real print test — flip this if the label prints upside down.
///
/// Pixel bits are written <b>inverted</b> relative to <see cref="MonochromeBitmap"/>'s "1 = dark/
/// printed pixel" convention — a written <c>0</c> is a dark/printed pixel, <c>1</c> is light — and
/// the palette is index 0 = black, index 1 = white to match. Confirmed on real Argox hardware:
/// sending bits in the "natural" sense (1 = dark) with a white/black palette printed fully inverted
/// (black background, white content) — this printer's BMP parser doesn't consult the embedded
/// palette at all and instead assumes a fixed 0 = black, 1 = white convention.
/// </summary>
public static class MonochromeBmpEncoder
{
    public static byte[] Encode(MonochromeBitmap pixels, bool reverseRowOrder = true)
    {
        const int fileHeaderSize = 14;
        const int infoHeaderSize = 40;
        const int paletteSize = 2 * 4; // 2 colors, 4 bytes (B,G,R,0) each
        const int pixelDataOffset = fileHeaderSize + infoHeaderSize + paletteSize;

        var rowBytesPadded = ((pixels.WidthPx + 31) / 32) * 4;
        var pixelDataSize = rowBytesPadded * pixels.HeightPx;
        var fileSize = pixelDataOffset + pixelDataSize;

        var bytes = new byte[fileSize];

        // BITMAPFILEHEADER
        bytes[0] = 0x42; // 'B'
        bytes[1] = 0x4D; // 'M'
        WriteUInt32(bytes, 2, (uint)fileSize);
        WriteUInt32(bytes, 6, 0); // reserved
        WriteUInt32(bytes, 10, pixelDataOffset);

        // BITMAPINFOHEADER
        WriteUInt32(bytes, 14, infoHeaderSize);
        WriteInt32(bytes, 18, pixels.WidthPx);
        WriteInt32(bytes, 22, pixels.HeightPx); // always positive — see class remarks.
        WriteUInt16(bytes, 26, 1); // planes
        WriteUInt16(bytes, 28, 1); // bit count (1bpp)
        WriteUInt32(bytes, 30, 0); // compression: BI_RGB
        WriteUInt32(bytes, 34, (uint)pixelDataSize);
        WriteInt32(bytes, 38, 0); // x pixels/meter
        WriteInt32(bytes, 42, 0); // y pixels/meter
        WriteUInt32(bytes, 46, 2); // colors used
        WriteUInt32(bytes, 50, 2); // important colors

        // Palette: index 0 = black, index 1 = white — inverted from the more "obvious" white/black
        // order to match the inverted bits below.
        var offset = 54;
        bytes[offset] = 0x00; bytes[offset + 1] = 0x00; bytes[offset + 2] = 0x00; bytes[offset + 3] = 0x00;
        offset += 4;
        bytes[offset] = 0xFF; bytes[offset + 1] = 0xFF; bytes[offset + 2] = 0xFF; bytes[offset + 3] = 0x00;

        // Pixel data, each row padded with zero bytes to a 4-byte boundary. Every source byte is
        // bitwise-inverted on the way out — see the real-hardware finding in the class remarks.
        var unpaddedRowBytes = (pixels.WidthPx + 7) / 8;
        for (var y = 0; y < pixels.HeightPx; y++)
        {
            var sourceRow = reverseRowOrder ? pixels.Rows[pixels.HeightPx - 1 - y] : pixels.Rows[y];
            var rowOffset = pixelDataOffset + y * rowBytesPadded;
            for (var i = 0; i < unpaddedRowBytes; i++)
            {
                bytes[rowOffset + i] = (byte)(sourceRow[i] ^ 0xFF);
            }

            // Remaining bytes in the padded row are already zero (default-initialized) — outside the
            // declared width, so never actually read as pixels by a spec-compliant BMP reader.
        }

        return bytes;
    }

    private static void WriteUInt32(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
        bytes[offset + 2] = (byte)(value >> 16);
        bytes[offset + 3] = (byte)(value >> 24);
    }

    private static void WriteInt32(byte[] bytes, int offset, int value) => WriteUInt32(bytes, offset, unchecked((uint)value));

    private static void WriteUInt16(byte[] bytes, int offset, ushort value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
    }
}
