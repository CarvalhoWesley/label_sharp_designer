using System.Text.Json;

namespace LabelSharpDesignerCore.App;

/// <summary>Loads/saves <see cref="EditorLayoutSettings"/> at
/// <c>%APPDATA%\LabelSharpDesignerCore\editor-layout.json</c> — mirrors <see cref="PrintSettingsStore"/>'s
/// shape for the same kind of small app-preferences file.</summary>
internal static class EditorLayoutSettingsStore
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LabelSharpDesignerCore",
        "editor-layout.json");

    /// <summary>Failure-safe: a missing, corrupt, or unreadable settings file just falls back to
    /// defaults instead of blocking the editor from opening.</summary>
    public static EditorLayoutSettings Load()
    {
        try
        {
            return JsonSerializer.Deserialize<EditorLayoutSettings>(File.ReadAllText(FilePath)) ?? new EditorLayoutSettings();
        }
        catch
        {
            return new EditorLayoutSettings();
        }
    }

    /// <summary>Failure-safe: a failure to persist layout preferences should never interrupt resizing
    /// or collapsing a panel.</summary>
    public static void Save(EditorLayoutSettings settings)
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
