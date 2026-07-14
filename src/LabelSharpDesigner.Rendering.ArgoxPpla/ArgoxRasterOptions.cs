namespace LabelSharpDesigner.Rendering.ArgoxPpla;

/// <summary>Settings specific to <see cref="PplaRasterBuilder"/>. Wraps <see cref="ArgoxRendererOptions"/>
/// instead of duplicating its fields — darkness/copies/transferType/offsets/feedOffsetMm all still
/// apply the same way to a rasterized job as to a native-command one; only how the label's *content*
/// gets to the printer differs.</summary>
public sealed record ArgoxRasterOptions
{
    public ArgoxRendererOptions Base { get; init; } = new();

    /// <summary>PPLA <c>&lt;STX&gt;I</c> memory module bank select (a single letter). The Datamax
    /// Class Series 2 Programmer's Manual's own <c>&lt;STX&gt;I</c> example uses <c>D</c> — not
    /// confirmed against every Argox model.</summary>
    public string MemoryBank { get; init; } = "D";

    /// <summary>Name the rasterized label image is downloaded and referenced under (PPLA
    /// <c>&lt;STX&gt;I</c>'s name field, max 16 chars). Reused every print job — nothing is meant to
    /// persist across jobs, each one re-uploads a fresh image under the same name.</summary>
    public string ImageName { get; init; } = "LBL";

    /// <summary>Luminance threshold (0-255) below which a pixel is considered "dark"/printed.</summary>
    public int Threshold { get; init; } = 128;

    /// <summary>Controls the printed row order — see <see cref="MonochromeBmpEncoder"/>'s
    /// <c>reverseRowOrder</c> parameter, which this maps straight through to. Default
    /// <see langword="false"/> (natural/unreversed row order); flip if the label prints upside down.</summary>
    public bool ReverseRowOrder { get; init; }

    /// <summary>Mirrors every row left-right before packing — independent of
    /// <see cref="ReverseRowOrder"/> (which only reorders whole rows vertically). Confirmed necessary
    /// on real Argox hardware: a raster print came out upside down *and* mirrored left-right at the
    /// same time (equivalent to a 180° rotation); <see langword="true"/> (the default) is what fixed
    /// the left-right mirroring.</summary>
    public bool MirrorHorizontal { get; init; } = true;

    /// <summary>How many times finer a resolution to render the label at before downsampling to the
    /// final addressable-dot grid — proper anti-aliasing instead of point-sampling a render already
    /// produced at the final (low) resolution. Purely a rendering-quality knob: it never changes the
    /// final image's pixel dimensions, so it can't affect the physical size printed. Higher costs more
    /// render time for a fixed-size label — 4 is a reasonable default. <c>1</c> disables it.</summary>
    public int Supersample { get; init; } = 4;

    /// <summary>Whether to send the rasterized image at the print head's full native resolution (PPLA
    /// <c>D11</c>, one addressable dot per physical dot) instead of <see cref="PplaFields.PplaDotMultiplier"/>'s
    /// DPI-based default (<c>D22</c> on a 203 DPI head). The manual documents <c>D22</c> as only 203
    /// DPI's *default*, not its only valid setting — and a raster-only job has no barcode
    /// module-width field (the one other place the multiplier matters) for <c>D11</c> to desync with,
    /// unlike a native-command job.</summary>
    public bool FullResolution { get; init; } = true;
}
