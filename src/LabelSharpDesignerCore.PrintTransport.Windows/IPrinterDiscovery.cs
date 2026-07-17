namespace LabelSharpDesignerCore.PrintTransport.Windows;

/// <summary>Lists printers an <see cref="IPrintTransport"/> could send to.</summary>
public interface IPrinterDiscovery
{
    /// <summary>Returns the printer names (<see cref="IPrintTransport.Send"/>'s <c>target</c>)
    /// available right now.</summary>
    IReadOnlyList<string> ListAvailable();
}
