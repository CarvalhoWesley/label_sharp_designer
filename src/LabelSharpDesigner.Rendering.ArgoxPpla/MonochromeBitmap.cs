namespace LabelSharpDesigner.Rendering.ArgoxPpla;

/// <summary>A monochrome (1 bit per pixel) raster, independent of any file format — deliberately
/// SkiaSharp-agnostic so <see cref="MonochromeBmpEncoder"/> stays pure and testable without needing
/// a rendered image.</summary>
public sealed record MonochromeBitmap
{
    public required int WidthPx { get; init; }

    public required int HeightPx { get; init; }

    /// <summary>Top-down, one entry per scanline (<c>Rows.Count == HeightPx</c>). Each row is packed
    /// MSB-first, 1 bit per pixel, 1 = a "dark"/printed pixel, unpadded
    /// (<c>row.Length == (WidthPx + 7) / 8</c>) — row padding to a byte-alignment boundary is a BMP
    /// file-format concern, not this format-agnostic pixel grid's.</summary>
    public required IReadOnlyList<byte[]> Rows { get; init; }

    /// <summary>Converts an RGBA buffer (4 bytes/pixel, row-major, top-down, unpadded) to a
    /// <see cref="MonochromeBitmap"/> by thresholding each pixel's luminance.
    ///
    /// No dithering: label content is text/bars/lines — near-solid black on white — where a hard
    /// threshold reproduces the source faithfully. Assumes the source is already fully opaque (the
    /// renderer always paints an opaque background before any element), so alpha is ignored.
    /// </summary>
    public static MonochromeBitmap FromRgba(byte[] rgba, int widthPx, int heightPx, int threshold = 128, bool mirrorHorizontal = false)
    {
        var rowBytes = (widthPx + 7) / 8;
        var rows = new byte[heightPx][];

        for (var y = 0; y < heightPx; y++)
        {
            var row = new byte[rowBytes];
            var rowOffset = y * widthPx * 4;
            for (var x = 0; x < widthPx; x++)
            {
                var pixelOffset = rowOffset + x * 4;
                var r = rgba[pixelOffset];
                var g = rgba[pixelOffset + 1];
                var b = rgba[pixelOffset + 2];
                var luminance = 0.299 * r + 0.587 * g + 0.114 * b;
                if (luminance < threshold)
                {
                    // mirrorHorizontal reverses which output column a source pixel lands in (a true
                    // left-right mirror of the whole row), not the bit order within a byte.
                    var column = mirrorHorizontal ? widthPx - 1 - x : x;
                    var byteIndex = column / 8;
                    var bitFromMsb = 7 - (column % 8);
                    row[byteIndex] |= (byte)(1 << bitFromMsb);
                }
            }

            rows[y] = row;
        }

        return new MonochromeBitmap { WidthPx = widthPx, HeightPx = heightPx, Rows = rows };
    }
}
