using System.ComponentModel;
using System.Diagnostics;

namespace LabelSharpDesignerCore.LegacySampleApp.Printing;

/// <summary>Sends PDF bytes (from <c>Rendering.Pdf</c>) to a Windows printer through the system's own
/// default PDF handler, via the <c>printto</c>/<c>print</c> shell verbs — works with any installed
/// printer, not just direct-command thermal printers.</summary>
internal sealed class WindowsPdfPrintTransport
{
    public int Copies { get; init; } = 1;

    public void Send(byte[] bytes, string? target = null)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"LabelSharpDesignerCore_{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(tempPath, bytes);

        for (var i = 0; i < Math.Max(Copies, 1); i++)
        {
            PrintOnce(tempPath, target);
        }
    }

    private static void PrintOnce(string pdfPath, string? target)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = pdfPath,
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            Verb = target is not null ? "printto" : "print",
        };

        if (target is not null)
        {
            startInfo.Arguments = $"\"{target}\"";
        }

        try
        {
            using var process = Process.Start(startInfo);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            throw new PrintTransportException(
                $"Falha ao imprimir PDF via o visualizador padrão do sistema para '{target ?? "impressora padrão"}'.", ex);
        }
    }
}
