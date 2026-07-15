using System.Text.Json;

namespace LabelSharpDesigner.LegacySampleApp.Printing;

/// <summary>Loads/saves <see cref="PrintColumnsSettings"/> at
/// <c>%APPDATA%\LabelSharpDesigner\LegacySampleApp\print-columns-settings.json</c>.</summary>
internal static class PrintColumnsSettingsStore
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LabelSharpDesigner",
        "LegacySampleApp",
        "print-columns-settings.json");

    /// <summary>Failure-safe: a missing, corrupt, or unreadable settings file just falls back to "no
    /// persisted value yet" instead of blocking the print screen from opening.</summary>
    public static PrintColumnsSettings Load()
    {
        try
        {
            return JsonSerializer.Deserialize<PrintColumnsSettings>(File.ReadAllText(FilePath)) ?? new PrintColumnsSettings();
        }
        catch
        {
            return new PrintColumnsSettings();
        }
    }

    /// <summary>Failure-safe: a failure to persist this preference should never block a print that
    /// already succeeded.</summary>
    public static void Save(PrintColumnsSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings));
        }
        catch
        {
        }
    }
}
