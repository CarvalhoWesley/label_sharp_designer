using LabelSharpDesigner.Core.Elements;

namespace LabelSharpDesigner.Rendering.ArgoxPpla;

/// <summary>
/// Field-formatting helpers for PPLA, ported from the original Flutter project's
/// <c>label_renderer_argox</c> package. Every mapping here (orientation codes, ASD font sizes,
/// barcode type letters, the <c>0-9,A-O</c> scale alphabet, the hundredths-of-an-inch unit
/// conversions) was cross-checked against the Datamax Class Series 2 Programmer's Manual and, in
/// several documented cases below, corrected against real Argox hardware after an initial
/// spec-only reading printed wrong — see each function's remarks for what was actually found on a
/// physical printer versus what the manual alone says.
/// </summary>
public static class PplaFields
{
    /// <summary>netstandard2.0 has no <c>Math.Clamp</c> — this project's lowest target, so the
    /// legacy ASP.NET Framework bridge can reference it directly.</summary>
    internal static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

    /// <summary>Zero-pads <paramref name="value"/> to <paramref name="width"/> digits, e.g.
    /// <c>PplaDigits(7, 4) == "0007"</c>. PPLA fixed-width fields never carry a sign, so negative
    /// input (which shouldn't occur — resolved element geometry is always non-negative) is clamped
    /// to zero rather than emitting a '-' that would corrupt the fixed column layout.</summary>
    public static string PplaDigits(int value, int width) => Clamp(value, 0, int.MaxValue).ToString().PadLeft(width, '0');

    /// <summary>PPLA only supports four fixed orientations (no arbitrary rotation);
    /// <paramref name="rotationDegrees"/> is snapped to the nearest multiple of 90° and mapped to
    /// the printer's own (non-sequential) code: <c>1</c>=0°, <c>4</c>=90°, <c>3</c>=180°, <c>2</c>=270°.</summary>
    public static string PplaOrientationCode(double rotationDegrees)
    {
        var normalized = ((rotationDegrees % 360) + 360) % 360;
        var steps = (int)Math.Round(normalized / 90) % 4;
        string[] codes = ["1", "4", "3", "2"];
        return codes[steps];
    }

    /// <summary>Encodes a magnification factor (<c>0</c>-<c>24</c>) as the single character PPLA
    /// uses for h/v text scale and barcode bar widths: <c>0</c>-<c>9</c> then <c>A</c>-<c>O</c>.</summary>
    public static string PplaScaleCode(int scale)
    {
        var clamped = Clamp(scale, 0, 24);
        return clamped < 10 ? clamped.ToString() : ((char)('A' + (clamped - 10))).ToString();
    }

    /// <summary>Point sizes of the internal ASD smooth font (font type <c>9</c>) available at
    /// 203 DPI via the <c>Ann</c> size-selector code, per the Datamax Class Series 2 Programmer's
    /// Manual, Table C-6 ("Internal Bitmapped (Smooth Font) 9 Size Chart"). 4pt and 72pt only exist
    /// at 300 DPI and above — see <see cref="PplaAsdFontSizesPt300Plus"/> — so a 203 DPI head's
    /// usable range starts at 6pt.</summary>
    public static readonly IReadOnlyList<int> PplaAsdFontSizesPt = [6, 8, 10, 12, 14, 18, 24, 30, 36, 48];

    /// <summary>Same as <see cref="PplaAsdFontSizesPt"/> but for 300/400/600 DPI heads, which also
    /// support 4pt and 72pt per Table C-6.</summary>
    public static readonly IReadOnlyList<int> PplaAsdFontSizesPt300Plus = [4, 6, 8, 10, 12, 14, 18, 24, 30, 36, 48, 72];

    /// <summary>
    /// Picks the ASD smooth font size code (<c>0nn</c>, e.g. <c>004</c> for 12pt) whose point size
    /// is closest to <paramref name="fontSizeDots"/> converted to points at <paramref name="dpi"/>.
    ///
    /// PPLA has no arbitrary-size text — only a fixed ladder of ASD sizes (plus unrelated
    /// fixed-size bitmap fonts) — so an exact match is not always possible; this rounds to the
    /// nearest available size rather than rejecting the element.
    ///
    /// The real <c>0nn</c> numeric codes only cover <c>001</c>-<c>010</c> (6pt-48pt); <c>000</c> is
    /// reserved for 300 DPI+ only (4pt), and there is no 16pt size at all. An out-of-spec code like
    /// <c>000</c> sent to a 203 DPI printer falls back to a much larger default font — confirmed on
    /// real Argox hardware printing text far bigger than configured. The manual's alternative
    /// <c>Ann</c> alpha form (documented as DPI-independent) was also tried and found to not be
    /// recognized on at least one real Argox model (OS-214 Plus), reproducing the same oversized
    /// fallback — so this uses the numeric form with the table Table C-6 actually documents: index
    /// within <see cref="PplaAsdFontSizesPt"/>/<see cref="PplaAsdFontSizesPt300Plus"/> plus one at
    /// &lt;300 DPI (<c>001</c>-<c>010</c>), or the bare index at &gt;=300 DPI (<c>000</c>-<c>011</c>,
    /// since <c>000</c> covers the 300-DPI-only 4pt size there).
    /// </summary>
    public static string PplaAsdFontSubtype(int fontSizeDots, int dpi)
    {
        var pointSize = fontSizeDots * 72.0 / dpi;
        var sizes = dpi >= 300 ? PplaAsdFontSizesPt300Plus : PplaAsdFontSizesPt;
        var closestIndex = 0;
        var closestDiff = Math.Abs(pointSize - sizes[0]);
        for (var i = 1; i < sizes.Count; i++)
        {
            var diff = Math.Abs(pointSize - sizes[i]);
            if (diff < closestDiff)
            {
                closestDiff = diff;
                closestIndex = i;
            }
        }

        var code = dpi >= 300 ? closestIndex : closestIndex + 1;
        return code.ToString().PadLeft(3, '0');
    }

    /// <summary>
    /// How many physical print-head dots each PPLA "addressable dot" covers — <c>2</c> (a 2x2
    /// physical block per address) on 203 DPI heads, <c>1</c> (no doubling) on 300/400/600 DPI
    /// heads. Backs the <c>Dnn</c> dot-size command; also needed by <see cref="PplaRasterBuilder"/>
    /// to size a rasterized image in *addressable* dots rather than physical dots — a native
    /// text/barcode/shape command's position and size are expressed in hundredths of an inch (an
    /// absolute physical unit, unaffected by this multiplier), but a downloaded image's pixels map
    /// 1:1 to addressable dots, so a raster image sized in physical (undoubled) dots ends up
    /// printing at this multiplier's factor too large — confirmed on real Argox hardware (a
    /// 40x60mm label rasterized at the nominal 320x480px printed at roughly double size on a
    /// 203 DPI head).
    /// </summary>
    public static int PplaDotMultiplier(int dpi) => dpi == 203 ? 2 : 1;

    /// <summary>
    /// PPLA's <c>D</c> command (dot width/height multiplier) — a per-model default documented in
    /// the Datamax Class Series 2 Programmer's Manual: <c>D11</c> (1x1, no doubling) for
    /// 300/400/600 DPI print heads, <c>D22</c> (2x2) for 203 DPI print heads. Getting this wrong
    /// desyncs the physical size of a printed dot from the mm→dots math the rest of this renderer
    /// assumes — found after the first real-hardware print test came out uniformly oversized on a
    /// 203 DPI Argox printer (an earlier version hardcoded <c>D11</c> regardless of DPI).
    /// </summary>
    public static string PplaDotSizeCommand(int dpi)
    {
        var m = PplaDotMultiplier(dpi);
        return $"D{m}{m}";
    }

    /// <summary>
    /// Converts <paramref name="dots"/> (at <paramref name="dpi"/> dots/inch) to hundredths of an
    /// inch — the unit PPLA actually uses for every row/column position field and every line/box
    /// dimension field, confirmed against the Datamax Class Series 2 Programmer's Manual: "Field
    /// data is interpreted in hundredths of an inch" (row/column), "all measurements are
    /// interpreted as inches/100" (lines and boxes), and the barcode height field's documented
    /// .01"-9.99" range. Embedding raw dot counts in these fields instead prints ~2x too large on
    /// real 203 DPI hardware (a dot is about twice as fine as a hundredth of an inch at that
    /// resolution) — found via a real print test, not from the spec alone.
    ///
    /// Not used for barcode wide/narrow bar width (<c>c</c>/<c>d</c> fields) — those are explicitly
    /// documented as dots, unlike everything else here.
    /// </summary>
    public static int PplaHundredthsOfInch(int dots, int dpi) => (int)Math.Round(dots * 100.0 / dpi);

    /// <summary>Converts <paramref name="mm"/> straight to hundredths of an inch, for values (like
    /// a manual calibration offset) that start out in millimeters rather than document dots — skips
    /// the dots round-trip <see cref="PplaHundredthsOfInch"/> does, avoiding an extra rounding step.</summary>
    public static int PplaMmToHundredthsOfInch(double mm) => (int)Math.Round(mm * 100 / 25.4);

    /// <summary>One PPLA type letter per <see cref="BarcodeSymbology"/> this project's <c>Barcode</c>
    /// package can encode. Uppercase shows the human-readable text under the bars; lowercase
    /// suppresses it — see <see cref="PplaBarcodeTypeCode"/>.</summary>
    public static readonly IReadOnlyDictionary<BarcodeSymbology, char> PplaBarcodeTypeLetters = new Dictionary<BarcodeSymbology, char>
    {
        [BarcodeSymbology.Code39] = 'A',
        [BarcodeSymbology.Upc] = 'B',
        [BarcodeSymbology.Itf] = 'D',
        [BarcodeSymbology.Code128] = 'E',
        [BarcodeSymbology.Ean13] = 'F',
        [BarcodeSymbology.Ean8] = 'G',
        [BarcodeSymbology.Codabar] = 'I',
    };

    /// <summary>The PPLA type letter for <paramref name="symbology"/>, or <see langword="null"/> if
    /// PPLA has no known mapping for it — callers should skip the element rather than emit an
    /// invalid command.</summary>
    public static string? PplaBarcodeTypeCode(BarcodeSymbology symbology, bool humanReadable)
    {
        if (!PplaBarcodeTypeLetters.TryGetValue(symbology, out var letter))
        {
            return null;
        }

        return humanReadable ? letter.ToString() : char.ToLowerInvariant(letter).ToString();
    }
}
