using System.Drawing.Printing;

namespace LabelSharpDesignerCore.PrintTransport.Windows;

/// <summary>
/// Sends raw bytes (e.g. PPLA from <c>Rendering.ArgoxPpla</c>) straight to a Windows printer's
/// spooler via <see cref="RawPrinterHelper"/>, bypassing Windows' own driver rendering — the printer
/// receives exactly the bytes it was given. <c>target</c> is a Windows printer name; <see langword="null"/>
/// uses the system default printer.
/// </summary>
public sealed class WindowsRawPrintTransport : IPrintTransport
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
