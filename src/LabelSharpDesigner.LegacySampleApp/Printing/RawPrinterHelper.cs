using System.Runtime.InteropServices;

namespace LabelSharpDesigner.LegacySampleApp.Printing;

/// <summary>
/// Sends a raw byte stream straight to a Windows printer's spooler, bypassing the printer driver
/// entirely — the classic "RawPrinterHelper" P/Invoke pattern over <c>winspool.drv</c>, used for
/// thermal-printer command languages (PPLA) the driver would otherwise try to reinterpret as a
/// document to render.
///
/// <para>This app cannot reference <c>LabelSharpDesigner.PrintTransport.Windows</c> (it targets
/// <c>net9.0-windows</c> only — a different, incompatible runtime from this project's <c>net48</c>),
/// so this is a from-scratch net48-native reimplementation of the same technique, not a copy-paste of
/// that project's internals.</para>
/// </summary>
internal static class RawPrinterHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DocInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string DocName;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? OutputFile;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string DataType;
    }

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "OpenPrinterW")]
    private static extern bool OpenPrinter(string printerName, out IntPtr phPrinter, IntPtr pDefault);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "StartDocPrinterW")]
    private static extern int StartDocPrinter(IntPtr hPrinter, int level, ref DocInfo pDocInfo);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, byte[] pBytes, int dwCount, out int dwWritten);

    public static void SendBytesToPrinter(string printerName, byte[] bytes, string documentName = "LabelSharpDesigner")
    {
        if (!OpenPrinter(printerName, out var hPrinter, IntPtr.Zero))
        {
            throw new PrintTransportException($"OpenPrinter falhou para a impressora '{printerName}' (erro Win32 {Marshal.GetLastWin32Error()}).");
        }

        try
        {
            var docInfo = new DocInfo { DocName = documentName, OutputFile = null, DataType = "RAW" };
            if (StartDocPrinter(hPrinter, 1, ref docInfo) == 0)
            {
                throw new PrintTransportException($"StartDocPrinter falhou para '{printerName}' (erro Win32 {Marshal.GetLastWin32Error()}).");
            }

            try
            {
                if (!StartPagePrinter(hPrinter))
                {
                    throw new PrintTransportException($"StartPagePrinter falhou para '{printerName}' (erro Win32 {Marshal.GetLastWin32Error()}).");
                }

                try
                {
                    if (!WritePrinter(hPrinter, bytes, bytes.Length, out var written) || written != bytes.Length)
                    {
                        throw new PrintTransportException($"WritePrinter falhou ou escreveu apenas {written}/{bytes.Length} bytes (erro Win32 {Marshal.GetLastWin32Error()}).");
                    }
                }
                finally
                {
                    EndPagePrinter(hPrinter);
                }
            }
            finally
            {
                EndDocPrinter(hPrinter);
            }
        }
        finally
        {
            ClosePrinter(hPrinter);
        }
    }
}
