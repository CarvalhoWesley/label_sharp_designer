namespace LabelSharpDesignerCore.PrintTransport.Windows;

/// <summary>
/// Sends already-rendered bytes (PPLA, PDF, ...) to a physical printer, over whatever channel a
/// concrete backend implements. This is the only contract the rest of the app depends on for print
/// transport — it deliberately knows nothing about <c>LabelDocument</c>, <c>ResolvedDocument</c> or
/// any <c>Rendering.*</c> project, so it stays reusable regardless of which renderer produced the bytes.
/// </summary>
public interface IPrintTransport
{
    /// <summary>Sends <paramref name="bytes"/> to the printer. <paramref name="target"/> identifies
    /// which printer to use (a Windows printer name); <see langword="null"/> means "the system's
    /// default printer".</summary>
    void Send(byte[] bytes, string? target = null);
}
