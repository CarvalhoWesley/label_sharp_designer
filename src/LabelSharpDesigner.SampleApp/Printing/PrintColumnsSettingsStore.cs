using System.Text.Json;

namespace LabelSharpDesigner.SampleApp.Printing;

/// <summary>Loads/saves <see cref="PrintColumnsSettings"/> at
/// <c>%APPDATA%\LabelSharpDesigner\SampleApp\print-columns-settings.json</c> — mirrors the main App's
/// <c>PrintSettingsStore</c> pattern for a single small preferences file.</summary>
internal static class PrintColumnsSettingsStore
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LabelSharpDesigner",
        "SampleApp",
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

    /// <summary>Failure-safe: a failure to persist this preference (e.g. a locked/read-only profile
    /// folder) should never block the print that just succeeded.</summary>
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
