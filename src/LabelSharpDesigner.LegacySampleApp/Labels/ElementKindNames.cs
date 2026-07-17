namespace LabelSharpDesigner.LegacySampleApp.Labels;

/// <summary>
/// The element kind names the plugin's editor understands for <c>LaunchRequest.AllowedElementKinds</c>
/// — plain strings because this project can't reference <c>LabelSharpDesigner.App</c>'s own
/// <c>NewElementKind</c> enum (a <c>net9.0-windows</c>-only assembly, unreachable from this
/// <c>net48</c> project; see INTEGRATION.md, "Caminho B"). Names must match
/// <c>NewElementKind.ToString()</c> exactly — the satellite process parses them case-insensitively but
/// otherwise as-is, silently dropping anything it doesn't recognize.
/// </summary>
internal static class ElementKindNames
{
    /// <summary>Display label (Portuguese) per element kind name, in the order offered in this app's
    /// own UI — mirrors <c>NewElementFactory.Label</c> on the plugin side.</summary>
    public static readonly IReadOnlyList<(string Name, string DisplayLabel)> All = new (string, string)[]
    {
        ("Text", "Texto"),
        ("Rectangle", "Retângulo"),
        ("Ellipse", "Elipse"),
        ("Circle", "Círculo"),
        ("Line", "Linha"),
        ("Barcode", "Código de barras"),
        ("QrCode", "QR Code"),
        ("Date", "Data"),
        ("Time", "Hora"),
        ("Image", "Imagem"),
        ("Table", "Tabela"),
    };
}
