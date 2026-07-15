namespace LabelSharpDesigner.LegacySampleApp.Printing;

/// <summary>Thrown by this app's net48-native <see cref="WindowsRawPrintTransport"/>/
/// <see cref="WindowsPdfPrintTransport"/> when sending to a printer fails.</summary>
public sealed class PrintTransportException : Exception
{
    public PrintTransportException(string message) : base(message)
    {
    }

    public PrintTransportException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
