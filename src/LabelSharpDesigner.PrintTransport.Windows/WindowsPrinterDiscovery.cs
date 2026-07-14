using System.Drawing.Printing;

namespace LabelSharpDesigner.PrintTransport.Windows;

/// <summary>Lists printer names known to the Windows spooler — the <c>target</c> values
/// <see cref="WindowsRawPrintTransport"/>/<see cref="WindowsPdfPrintTransport"/> accept.</summary>
public sealed class WindowsPrinterDiscovery : IPrinterDiscovery
{
    public IReadOnlyList<string> ListAvailable()
    {
        var names = new List<string>();
        foreach (string name in PrinterSettings.InstalledPrinters)
        {
            names.Add(name);
        }

        return names;
    }
}
