using LabelSharpDesignerCore.Core.Document;

namespace LabelSharpDesignerCore.App;

/// <summary>The print dialog's last-used settings — printer, format, and every format-specific
/// option — persisted across app runs so reopening "Imprimir..." doesn't reset to hardcoded
/// defaults every time. An app-level preference (see <see cref="PrintSettingsStore"/>), not part of
/// any <see cref="LabelDocument"/>: it's about how you print, not what you're printing.</summary>
public sealed class PrintSettings
{
    public string? PrinterName { get; set; }
    public PrintDialogForm.PrintFormat Format { get; set; } = PrintDialogForm.PrintFormat.Pdf;

    /// <summary>The last-used value of the single "Cópias"/"Quantidade de etiquetas" control — see
    /// <see cref="PrintDialogForm"/>'s class doc for what it means on a single-column vs multi-column
    /// (<c>PageConfig.Columns &gt; 1</c>) label.</summary>
    public int Quantity { get; set; } = 1;

    public int Darkness { get; set; } = 10;
    public ArgoxTransferTypeSetting TransferType { get; set; } = ArgoxTransferTypeSetting.DirectThermal;

    /// <summary>Manual PPLA calibration (mm), found by trial print on a given physical printer —
    /// see <see cref="LabelSharpDesignerCore.Rendering.ArgoxPpla.ArgoxRendererOptions.OffsetXMm"/>/
    /// <c>OffsetYMm</c>/<c>FeedOffsetMm</c> for what each one does. PPLA bypasses the Windows driver
    /// entirely, so there's no way to read a printer's own calibration automatically.</summary>
    public double OffsetXMm { get; set; }
    public double OffsetYMm { get; set; }
    public double FeedOffsetMm { get; set; }

    public bool FullResolution { get; set; } = true;
    public bool MirrorHorizontal { get; set; } = true;
    public bool ReverseRowOrder { get; set; }
}

/// <summary>Mirrors <see cref="LabelSharpDesignerCore.Rendering.ArgoxPpla.ArgoxTransferType"/> — kept as
/// its own enum here rather than referencing that one directly so this settings file's on-disk shape
/// never has to change if the renderer's own enum is ever extended/reordered.</summary>
public enum ArgoxTransferTypeSetting
{
    DirectThermal,
    ThermalTransfer,
}
