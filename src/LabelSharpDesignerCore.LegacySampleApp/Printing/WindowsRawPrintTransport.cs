using System.Drawing.Printing;

namespace LabelSharpDesignerCore.LegacySampleApp.Printing;

/// <summary>Sends raw bytes (PPLA from <c>Rendering.ArgoxPpla</c>) straight to a Windows printer's
/// spooler via <see cref="RawPrinterHelper"/>. <paramref name="target"/>-less calls use the system
/// default printer.</summary>
internal sealed class WindowsRawPrintTransport
{
    public void Send(byte[] bytes, string? target = null)
    {
        var printerName = target ?? DefaultPrinterName();
        RawPrinterHelper.SendBytesToPrinter(printerName, bytes);
    }

    private static string DefaultPrinterName()
    {
        var name = new PrinterSettings().PrinterName;
        if (string.IsNullOrEmpty(name))
        {
            throw new PrintTransportException("Nenhuma impressora padrão configurada e nenhum destino informado.");
        }

        return name;
    }
}
