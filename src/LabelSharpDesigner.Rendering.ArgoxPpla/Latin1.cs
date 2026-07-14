namespace LabelSharpDesigner.Rendering.ArgoxPpla;

/// <summary>
/// Minimal Latin-1 (ISO-8859-1) byte encoding — a straight 1:1 code-point-to-byte mapping, written
/// by hand instead of using <c>System.Text.Encoding.Latin1</c> because that property doesn't exist
/// on netstandard2.0 (this project's lowest target, so the legacy ASP.NET Framework bridge can
/// reference it directly), and registering <c>CodePagesEncodingProvider</c> just for one always-simple
/// encoding would be a heavier dependency than this. PPLA command strings are single-byte control
/// codes (STX, CR) plus ASCII/Latin-1 text content, matching the encoding the original Flutter
/// project's renderer also uses for the same reason.
/// </summary>
internal static class Latin1
{
    public static byte[] GetBytes(string value)
    {
        var bytes = new byte[value.Length];
        for (var i = 0; i < value.Length; i++)
        {
            bytes[i] = (byte)value[i];
        }

        return bytes;
    }
}
