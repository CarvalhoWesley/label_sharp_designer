namespace LabelSharpDesignerCore.PrintTransport.Windows;

public sealed class PrintTransportException : Exception
{
    public PrintTransportException(string message)
        : base(message)
    {
    }

    public PrintTransportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
