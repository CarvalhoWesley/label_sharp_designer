using System.ComponentModel;
using System.Diagnostics;

namespace LabelSharpDesigner.PrintTransport.Windows;

/// <summary>
/// Sends PDF bytes (e.g. from <c>Rendering.Pdf</c>) to a Windows printer through the system's own
/// default PDF handler (Edge, Adobe Reader, ...) rather than a bundled PDF rasterizer — works with
/// any installed printer, not just direct-command thermal printers, at the cost of being a
/// best-effort shell-out: <c>target</c> is passed via the <c>printto</c> verb, which not every PDF
/// handler honors (some only support the default-printer-only <c>print</c> verb, used automatically
/// when <c>target</c> is <see langword="null"/>). The bytes are written to a temp file first (the
/// shell verb needs a path, not a byte stream) — that file is intentionally left behind rather than
/// deleted immediately after <see cref="Process.Start(ProcessStartInfo)"/> returns, since the external
/// viewer reads it asynchronously and deleting too early would make printing fail silently; it's
/// cleaned up the next time the OS clears %TEMP%.
/// </summary>
public sealed class WindowsPdfPrintTransport : IPrintTransport
{
    /// <summary>Number of copies to print. Unlike raw PPLA (whose byte stream already encodes its
    /// own copy count), this shells out to an external viewer once per copy — not a native driver
    /// "copies" setting, so it's slower than a real multi-copy print job for large counts.</summary>
    public int Copies { get; init; } = 1;

    public void Send(byte[] bytes, string? target = null)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"LabelSharpDesigner_{Guid.NewGuid():N}.pdf");
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
