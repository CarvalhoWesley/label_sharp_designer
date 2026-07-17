using System.Text.Json;

namespace LabelSharpDesignerCore.SampleApp.Printing;

/// <summary>Loads/saves the user-configured product-field ↔ label-variable bindings at
/// <c>%APPDATA%\LabelSharpDesignerCore\SampleApp\variable-field-mappings.json</c>, keyed by label id
/// (<see cref="LabelSharpDesignerCore.App.Library.LibraryEntry.Id"/>) so every saved label remembers its
/// own bindings independently, then by variable name within that label.</summary>
internal static class VariableFieldMappingStore
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LabelSharpDesignerCore",
        "SampleApp",
        "variable-field-mappings.json");

    /// <summary>Failure-safe: a missing, corrupt, or unreadable file just falls back to "nothing
    /// configured yet" instead of blocking the print screen from opening.</summary>
    public static Dictionary<string, Dictionary<string, ProductFieldSource>> Load()
    {
        try
        {
            var mappings = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, ProductFieldSource>>>(File.ReadAllText(FilePath));
            return mappings ?? new Dictionary<string, Dictionary<string, ProductFieldSource>>(StringComparer.Ordinal);
        }
        catch
        {
            return new Dictionary<string, Dictionary<string, ProductFieldSource>>(StringComparer.Ordinal);
        }
    }

    /// <summary>Failure-safe: a failure to persist this preference (e.g. a locked/read-only profile
    /// folder) should never block the print that just succeeded.</summary>
    public static void Save(Dictionary<string, Dictionary<string, ProductFieldSource>> mappings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(mappings));
        }
        catch
        {
        }
    }
}
