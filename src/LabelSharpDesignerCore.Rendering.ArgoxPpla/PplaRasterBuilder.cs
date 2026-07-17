using LabelSharpDesignerCore.Core.Layout;
using LabelSharpDesignerCore.Rendering.Canvas;
using SkiaSharp;
using static LabelSharpDesignerCore.Rendering.ArgoxPpla.PplaFields;

namespace LabelSharpDesignerCore.Rendering.ArgoxPpla;

/// <summary>
/// Converts a <see cref="ResolvedDocument"/> to Argox PPLA bytes the same way
/// <see cref="PplaCommandBuilder"/> does for the job-level framing (clear memory, label length,
/// darkness, copies), but instead of one native PPLA command per element, rasterizes the *entire*
/// label via <see cref="LabelCanvasRenderer"/> (real typography, filled shapes, QR codes) and sends
/// the result as a single monochrome image, via PPLA's <c>&lt;STX&gt;I</c> image-download command
/// plus a "5: Images" placement record.
///
/// This exists because PPLA's native text/shape commands have real, unavoidable limits (a fixed
/// internal font ladder, no fill, no true ellipse, no QR — see <see cref="PplaCommandBuilder"/>).
/// Rasterizing sidesteps all of that at the cost of a bigger job (an image instead of compact
/// commands) and losing the printer's own crisp native font rendering. Both builders stay available
/// side by side; this doesn't replace <see cref="PplaCommandBuilder"/>.
/// </summary>
public static class PplaRasterBuilder
{
    private const char Stx = '\x02';
    private const char Cr = '\r';

    public static byte[] Build(ResolvedDocument document, ArgoxRasterOptions? options = null)
    {
        options ??= new ArgoxRasterOptions();

        // A raster-only job has no barcode module-width field (the one other place
        // PplaDotMultiplier matters) for D11 to desync — see ArgoxRasterOptions.FullResolution — so
        // this can safely override the dot-size command's DPI-based default independently of what a
        // mixed native-command job would need.
        var dotMultiplier = options.FullResolution ? 1 : PplaDotMultiplier(document.Dpi);
        var bmpBytes = RasterizeToMonochromeBmp(document, options, dotMultiplier);

        var bytes = new List<byte>();
        bytes.AddRange(Latin1.GetBytes(PplaCommandBuilder.ClearMemoryCommand()));

        // <STX>I{bank}{format}{name}<CR> — data type field is omitted, meaning raw 8-bit binary
        // follows directly; no terminator is needed after a BMP payload (unlike the unrelated 7-bit-
        // ASCII image format, BMP is self-describing via its own header). Format is always 'b' — 'B'
        // ("flipped") is the designator tied to a negative biHeight, confirmed to hang real hardware
        // (see MonochromeBmpEncoder). Row order is controlled purely by ArgoxRasterOptions.
        // ReverseRowOrder, in the BMP bytes themselves, never via this field.
        const string format = "b";
        bytes.AddRange(Latin1.GetBytes($"{Stx}I{options.MemoryBank}{format}{options.ImageName}{Cr}"));
        bytes.AddRange(bmpBytes);

        bytes.AddRange(Latin1.GetBytes(PplaCommandBuilder.LabelFormatHeader(document, options.Base, dotMultiplierOverride: dotMultiplier)));

        // 1Ycd000ffffgggg<name><CR> — rotation 1 (fixed for images), width/height multiplier 1 (no
        // scaling: already rasterized at the exact target dot grid), row/column 0000/0000 (the image
        // *is* the whole label, so it sits at the label's own origin). Built from the same three
        // literal groups as the original renderer to keep the (15-character) fixed-width field count
        // visibly correct rather than hand-counted in one long literal.
        bytes.AddRange(Latin1.GetBytes("1Y11000" + "0000" + "0000" + options.ImageName + Cr));
        bytes.AddRange(Latin1.GetBytes(PplaCommandBuilder.Footer(options.Base)));

        return bytes.ToArray();
    }

    private static byte[] RasterizeToMonochromeBmp(ResolvedDocument document, ArgoxRasterOptions options, int dotMultiplier)
    {
        // A native text/barcode/shape command's position and size are already in hundredths of an
        // inch (an absolute physical unit unaffected by the dot-size command's doubling), but a
        // downloaded image's pixels map 1:1 to PPLA "addressable dots" — the final image must have
        // exactly one pixel per addressable dot, otherwise the label prints at dotMultiplier times
        // too large (confirmed on real hardware). Nothing stops rendering at a finer resolution and
        // averaging back down to that grid for better anti-aliased edges — that's what Supersample
        // controls.
        var addressableWidth = Math.Max(document.WidthDots / dotMultiplier, 1);
        var addressableHeight = Math.Max(document.HeightDots / dotMultiplier, 1);
        var supersample = Math.Max(options.Supersample, 1);
        var renderWidth = addressableWidth * supersample;
        var renderHeight = addressableHeight * supersample;

        using var surface = SKSurface.Create(new SKImageInfo(renderWidth, renderHeight, SKColorType.Bgra8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Scale(renderWidth / (float)document.WidthDots, renderHeight / (float)document.HeightDots);
        LabelCanvasRenderer.Render(canvas, document);
        canvas.Flush();

        using var renderedBitmap = SKBitmap.FromImage(surface.Snapshot());
        using var finalBitmap = supersample > 1
            ? renderedBitmap.Resize(new SKImageInfo(addressableWidth, addressableHeight, SKColorType.Bgra8888, SKAlphaType.Premul), new SKSamplingOptions(SKCubicResampler.Mitchell))
            : renderedBitmap;

        var rgba = ToRgba(finalBitmap);
        var monochrome = MonochromeBitmap.FromRgba(rgba, addressableWidth, addressableHeight, options.Threshold, options.MirrorHorizontal);
        return MonochromeBmpEncoder.Encode(monochrome, options.ReverseRowOrder);
    }

    private static byte[] ToRgba(SKBitmap bitmap)
    {
        var rgba = new byte[bitmap.Width * bitmap.Height * 4];
        var offset = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var color = bitmap.GetPixel(x, y);
                rgba[offset] = color.Red;
                rgba[offset + 1] = color.Green;
                rgba[offset + 2] = color.Blue;
                rgba[offset + 3] = color.Alpha;
                offset += 4;
            }
        }

        return rgba;
    }
}
