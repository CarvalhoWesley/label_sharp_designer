using System.Drawing.Printing;

namespace LabelSharpDesignerCore.LegacySampleApp.Printing;

/// <summary>Lists printer names known to the Windows spooler.</summary>
internal static class WindowsPrinterDiscovery
{
    public static IReadOnlyList<string> ListAvailable()
    {
        var names = new List<string>();
        foreach (string name in PrinterSettings.InstalledPrinters)
        {
            names.Add(name);
        }

        return names;
    }
}
