using System.Text.Json;

namespace LabelSharpDesigner.LegacySampleApp.Labels;

/// <summary>Loads/saves <see cref="EditorLauncherSettings"/> at
/// <c>%APPDATA%\LabelSharpDesigner\LegacySampleApp\editor-launcher-settings.json</c>.</summary>
internal static class EditorLauncherSettingsStore
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LabelSharpDesigner",
        "LegacySampleApp",
        "editor-launcher-settings.json");

    /// <summary>Failure-safe: a missing, corrupt, or unreadable settings file just falls back to "not
    /// configured yet" instead of blocking the app from opening.</summary>
    public static EditorLauncherSettings Load()
    {
        try
        {
            return JsonSerializer.Deserialize<EditorLauncherSettings>(File.ReadAllText(FilePath)) ?? new EditorLauncherSettings();
        }
        catch
        {
            return new EditorLauncherSettings();
        }
    }

    /// <summary>Failure-safe: a failure to persist this preference should never block whatever action
    /// just succeeded.</summary>
    public static void Save(EditorLauncherSettings settings)
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
