namespace LabelSharpDesigner.LegacySampleApp.Labels;

/// <summary>Where this app expects to find the published <c>LabelSharpDesigner.App.exe</c> satellite
/// (see INTEGRATION.md §3.1) — an app-level preference, not part of any <c>LabelDocument</c>.</summary>
public sealed class EditorLauncherSettings
{
    public string? AppExecutablePath { get; set; }
}
