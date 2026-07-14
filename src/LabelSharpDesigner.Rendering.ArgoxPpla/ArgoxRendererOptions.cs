namespace LabelSharpDesigner.Rendering.ArgoxPpla;

/// <summary>Which Argox command language to emit.</summary>
public enum ArgoxDialect
{
    /// <summary>Printer Programming Language A — Datamax-compatible, mostly resolution-independent. Implemented.</summary>
    Ppla,

    /// <summary>Printer Programming Language B — Eltron/EPL2-compatible, resolution-dependent. Not
    /// implemented yet; <see cref="PplaCommandBuilder.Build"/> throws <see cref="NotSupportedException"/>
    /// for this dialect.</summary>
    Pplb,
}

/// <summary><c>DirectThermal</c> needs no ribbon; <c>ThermalTransfer</c> prints through a ribbon and
/// produces more durable labels.</summary>
public enum ArgoxTransferType
{
    DirectThermal,
    ThermalTransfer,
}

/// <summary>Settings specific to <see cref="PplaCommandBuilder"/> (and reused by
/// <see cref="PplaRasterBuilder"/> via <see cref="ArgoxRasterOptions.Base"/>).</summary>
public sealed record ArgoxRendererOptions
{
    public ArgoxDialect Dialect { get; init; } = ArgoxDialect.Ppla;

    /// <summary>Heat/darkness value, <c>H02</c>-<c>H20</c> in PPLA. Higher prints darker. Clamped to
    /// 2-20 when building the header.</summary>
    public int Darkness { get; init; } = 10;

    /// <summary>Number of copies to print, <c>Q0001</c>-<c>Q9999</c> in PPLA. Clamped to 1-9999 when
    /// building the footer.</summary>
    public int Copies { get; init; } = 1;

    public ArgoxTransferType TransferType { get; init; } = ArgoxTransferType.DirectThermal;

    /// <summary>Manual calibration offset (millimeters) added to every element's X/Y position before
    /// it's sent to the printer — compensates for a print head/gap-sensor mechanical offset specific
    /// to a given physical printer/media, the same kind of adjustment BarTender exposes as "print
    /// offset". There's no way to read this from the printer or the Windows driver (PPLA goes out as
    /// a raw byte stream, bypassing the driver entirely), so it has to be a user-supplied value found
    /// by trial print.</summary>
    public double OffsetXMm { get; init; }

    public double OffsetYMm { get; init; }

    /// <summary>Manual calibration offset (millimeters) added to the label length used in the
    /// label-length command — controls how far the printer physically feeds per label cycle, not
    /// where content is drawn within it (that's <see cref="OffsetXMm"/>/<see cref="OffsetYMm"/>).</summary>
    public double FeedOffsetMm { get; init; }
}
