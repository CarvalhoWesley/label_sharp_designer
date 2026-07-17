namespace LabelSharpDesignerCore.PrintTransport.Windows.Tests;

/// <summary>
/// <see cref="WindowsRawPrintTransport"/>/<see cref="WindowsPdfPrintTransport"/> themselves aren't
/// covered here — sending bytes to a real spooler or shelling out to a PDF viewer needs an actual
/// printer/handler to talk to, which isn't available in CI (matches the original Flutter project's
/// own documented decision for the equivalent Windows print-transport backends: validated manually
/// against real hardware, not by an automated suite). Discovery only reads the spooler's printer
/// list, which works — possibly returning an empty list — even with zero printers installed, so it's
/// safe to exercise here.
/// </summary>
public class WindowsPrinterDiscoveryTests
{
    [Fact]
    public void ListAvailable_DoesNotThrowRegardlessOfWhatPrintersAreInstalled()
    {
        var discovery = new WindowsPrinterDiscovery();

        var printers = discovery.ListAvailable();

        Assert.NotNull(printers);
    }
}
