using System.Text.Json;

namespace LabelSharpDesignerCore.App;

/// <summary>Loads/saves <see cref="PrintSettings"/> at <c>%APPDATA%\LabelSharpDesignerCore\print-settings.json</c>
/// — mirrors <c>LibraryRepository</c>'s use of the app data folder, but for a single small preferences
/// file rather than a directory of documents.</summary>
internal static class PrintSettingsStore
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LabelSharpDesignerCore",
        "print-settings.json");

    /// <summary>Failure-safe: a missing, corrupt, or unreadable settings file just falls back to
    /// defaults instead of blocking the print dialog from opening.</summary>
    public static PrintSettings Load()
    {
        try
        {
            return JsonSerializer.Deserialize<PrintSettings>(File.ReadAllText(FilePath)) ?? new PrintSettings();
        }
        catch
        {
            return new PrintSettings();
        }
    }

    /// <summary>Failure-safe: a failure to persist print preferences (e.g. a locked/read-only profile
    /// folder) should never block the print that just succeeded.</summary>
    public static void Save(PrintSettings settings)
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
