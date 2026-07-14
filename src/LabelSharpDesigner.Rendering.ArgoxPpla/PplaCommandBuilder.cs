using System.Text;
using LabelSharpDesigner.Core.Layout;
using LabelSharpDesigner.Rendering.Abstractions;
using static LabelSharpDesigner.Rendering.ArgoxPpla.PplaFields;

namespace LabelSharpDesigner.Rendering.ArgoxPpla;

/// <summary>
/// Converts a <see cref="ResolvedDocument"/> to Argox PPLA printer commands: one compact native
/// command per text/barcode/shape element. QR codes and images have no PPLA native-command
/// equivalent (2D-code support isn't cross-validated against a known byte format, and images need
/// PPLA's HEX/BMP graphics-download subsystem) — both are skipped here rather than emitting a guess;
/// use <see cref="PplaRasterBuilder"/> for a label containing either.
///
/// PPLA's coordinate system has its origin at the label's <b>bottom-left</b> corner with Y
/// increasing upward — the opposite of <see cref="ResolvedElement"/>, which (like every other
/// renderer in this project) uses a top-left origin with Y increasing downward. <see cref="ArgoxY"/>
/// is the one place that conversion happens; X needs no adjustment.
/// </summary>
public static class PplaCommandBuilder
{
    private const char Stx = '\x02';
    private const char Cr = '\r';

    public static byte[] Build(ResolvedDocument document, ArgoxRendererOptions? options = null)
    {
        options ??= new ArgoxRendererOptions();

        var builder = new StringBuilder();
        builder.Append(Header(document, options));
        foreach (var element in document.Elements.OrderBy(e => e.ZIndex))
        {
            builder.Append(EncodeElement(element, document, options));
        }

        builder.Append(Footer(options));
        return Latin1.GetBytes(builder.ToString());
    }

    private static string Header(ResolvedDocument document, ArgoxRendererOptions options) =>
        ClearMemoryCommand() + LabelFormatHeader(document, options);

    /// <summary><c>&lt;STX&gt;qA</c> — clears the printer's RAM image buffer. Split out from the
    /// combined header so <see cref="PplaRasterBuilder"/> can send this, then its own
    /// <c>&lt;STX&gt;I</c> image-download command, then <see cref="LabelFormatHeader"/> — the image
    /// download is a system-level command and must run before label-formatting mode is entered, but
    /// after the memory clear (a clear issued afterward would wipe the just-sent image).</summary>
    internal static string ClearMemoryCommand() => $"{Stx}qA{Cr}";

    /// <summary>Everything <see cref="Header"/> sends after <see cref="ClearMemoryCommand"/>:
    /// transfer type, label length, enter-label-format, dot size, darkness. Internal (not private)
    /// for the same raster-mode reuse reason as <see cref="ClearMemoryCommand"/>.
    ///
    /// <paramref name="dotMultiplierOverride"/> replaces <see cref="PplaFields.PplaDotMultiplier"/>'s
    /// DPI-based default for just the dot-size command — a raster-only job has no barcode
    /// module-width field (the one other place the multiplier matters) to desync, so
    /// <see cref="PplaRasterBuilder"/> uses this to request full print-head resolution even on a
    /// 203 DPI head that would otherwise default to the 2x2 doubling.</summary>
    internal static string LabelFormatHeader(ResolvedDocument document, ArgoxRendererOptions options, int? dotMultiplierOverride = null)
    {
        if (options.Dialect == ArgoxDialect.Pplb)
        {
            throw new NotSupportedException("ArgoxDialect.Pplb ainda não implementado — use ArgoxDialect.Ppla.");
        }

        var transferType = options.TransferType == ArgoxTransferType.ThermalTransfer ? '1' : '0';
        var lengthHundredthsOfInch = PplaDigits(
            PplaHundredthsOfInch(document.HeightDots, document.Dpi) + PplaMmToHundredthsOfInch(options.FeedOffsetMm), 4);
        var dotMultiplier = dotMultiplierOverride ?? PplaDotMultiplier(document.Dpi);
        var darkness = PplaDigits(Clamp(options.Darkness, 2, 20), 2);

        return $"{Stx}KI7{transferType}{Cr}" // transfer type
             + $"{Stx}c{lengthHundredthsOfInch}{Cr}" // label length
             + $"{Stx}L{Cr}" // enter label formatting mode
             + $"D{dotMultiplier}{dotMultiplier}{Cr}" // dot size, per print head DPI
             + $"H{darkness}{Cr}"; // darkness/heat
    }

    internal static string Footer(ArgoxRendererOptions options) =>
        $"Q{PplaDigits(Clamp(options.Copies, 1, 9999), 4)}{Cr}" // copies
      + $"E{Cr}"; // end job / print

    private static string EncodeElement(ResolvedElement element, ResolvedDocument document, ArgoxRendererOptions options) => element.Payload switch
    {
        ResolvedTextPayload payload => EncodeText(element, document, payload, options),
        ResolvedBarcodePayload payload => EncodeBarcode(element, document, payload, options),
        ResolvedShapePayload payload => EncodeShape(element, document, payload, options),
        ResolvedQrCodePayload => string.Empty, // no cross-validated PPLA 2D-code byte format — skip.
        ResolvedImagePayload => string.Empty, // needs PPLA's HEX/BMP graphics download subsystem — skip.
        ResolvedTablePayload => string.Empty, // no PPLA table primitive — skip, mirrors QR/image gaps.
        _ => string.Empty,
    };

    /// <summary>PPLA Y is measured from the *bottom* of the label to the *bottom* of the element's
    /// box — the mirror image of <see cref="ResolvedElement.YDots"/>, which is the *top* of the box
    /// measured from the label's top.</summary>
    private static int ArgoxY(ResolvedElement element, ResolvedDocument document) =>
        document.HeightDots - element.YDots - element.HeightDots;

    private static string EncodeText(ResolvedElement element, ResolvedDocument document, ResolvedTextPayload payload, ArgoxRendererOptions options)
    {
        var style = payload.Style;
        var orientation = PplaOrientationCode(element.RotationDegrees);
        const string fontType = "9"; // ASD smooth font — the only PPLA font family that scales to
                                      // arbitrary-ish sizes; no bold/italic or built-in bitmap font support.
        const string hScale = "1";
        const string vScale = "1";
        var fontSizeDots = (int)Math.Round(style.FontSizePt * document.Dpi / 72.0);
        var fontSubtype = PplaAsdFontSubtype(fontSizeDots, document.Dpi);
        var y = PplaDigits(PplaHundredthsOfInch(ArgoxY(element, document), document.Dpi) + PplaMmToHundredthsOfInch(options.OffsetYMm), 4);
        var x = PplaDigits(PplaHundredthsOfInch(element.XDots, document.Dpi) + PplaMmToHundredthsOfInch(options.OffsetXMm), 4);

        return $"{orientation}{fontType}{hScale}{vScale}{fontSubtype}{y}{x}{payload.Text}{Cr}";
    }

    private static string EncodeBarcode(ResolvedElement element, ResolvedDocument document, ResolvedBarcodePayload payload, ArgoxRendererOptions options)
    {
        var typeCode = PplaBarcodeTypeCode(payload.Symbology, payload.ShowText);
        if (typeCode is null)
        {
            return string.Empty; // unsupported symbology — skip.
        }

        var orientation = PplaOrientationCode(element.RotationDegrees);
        // Bar width (c/d fields) is the one dimension PPLA keeps in dots — everything else below is
        // hundredths of an inch. Unlike every other ResolvedPayload field, ResolvedBarcodePayload's
        // ModuleWidth is passed through from BarcodeElement.ModuleWidth unconverted by the layout
        // engine (millimeters, e.g. the 0.33 default) rather than pre-resolved to dots — Rendering.
        // Canvas never reads this field (it sizes the barcode raster from the element's own
        // width/height instead), so the mm→dots conversion had nowhere else to happen yet.
        var moduleWidthDots = payload.ModuleWidth * document.Dpi / 25.4;
        var barWidth = PplaScaleCode((int)Math.Round(moduleWidthDots));
        var height = PplaDigits(PplaHundredthsOfInch(element.HeightDots, document.Dpi), 3);
        var y = PplaDigits(PplaHundredthsOfInch(ArgoxY(element, document), document.Dpi) + PplaMmToHundredthsOfInch(options.OffsetYMm), 4);
        var x = PplaDigits(PplaHundredthsOfInch(element.XDots, document.Dpi) + PplaMmToHundredthsOfInch(options.OffsetXMm), 4);

        return $"{orientation}{typeCode}{barWidth}{barWidth}{height}{y}{x}{payload.Data}{Cr}";
    }

    private static string EncodeShape(ResolvedElement element, ResolvedDocument document, ResolvedShapePayload payload, ArgoxRendererOptions options)
    {
        var orientation = PplaOrientationCode(element.RotationDegrees);
        var y = PplaDigits(PplaHundredthsOfInch(ArgoxY(element, document), document.Dpi) + PplaMmToHundredthsOfInch(options.OffsetYMm), 4);
        var x = PplaDigits(PplaHundredthsOfInch(element.XDots, document.Dpi) + PplaMmToHundredthsOfInch(options.OffsetXMm), 4);
        var width = PplaDigits(PplaHundredthsOfInch(element.WidthDots, document.Dpi), 4);
        var height = PplaDigits(PplaHundredthsOfInch(element.HeightDots, document.Dpi), 4);

        if (payload.Kind == ShapeKind.Line)
        {
            // PPLA's line primitive is axis-aligned (a straight horizontal or vertical stroke sized
            // by width/height), unlike our diagonal line element.
            return $"{orientation}X11000{y}{x}l{width}{height}{Cr}";
        }

        // PPLA's box primitive is an outline only (no solid fill) and has no curve — ellipse/circle
        // render as their bounding box, and ResolvedShapePayload.FillColor has no PPLA equivalent.
        // Both are documented gaps, not silent data loss.
        var thicknessHundredths = Clamp(PplaHundredthsOfInch((int)Math.Round(payload.StrokeWidthDots), document.Dpi), 1, 999);
        var thickness = PplaDigits(thicknessHundredths, 4);
        return $"{orientation}X11000{y}{x}b{width}{height}{thickness}{thickness}{Cr}";
    }
}
