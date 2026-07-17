namespace LabelSharpDesignerCore.LegacySampleApp.Labels;

/// <summary>Where this app expects to find the published <c>LabelSharpDesignerCore.App.exe</c> satellite
/// (see INTEGRATION.md §3.1) — an app-level preference, not part of any <c>LabelDocument</c>.</summary>
public sealed class EditorLauncherSettings
{
    public string? AppExecutablePath { get; set; }

    /// <summary>Which element kinds (see <see cref="ElementKindNames"/>) the editor's "+ Adicionar"
    /// menu is allowed to offer — forwarded as <c>LaunchRequest.AllowedElementKinds</c>.
    /// <see langword="null"/> or empty (the default) means every kind is offered, unrestricted.</summary>
    public List<string>? AllowedElementKinds { get; set; }

    /// <summary>Whether the editor's layers sidebar is offered at all — forwarded as
    /// <c>LaunchRequest.ShowLayersPanel</c>. Defaults to <see langword="true"/>, same as the editor
    /// itself, so a settings file saved before this option existed still shows the panel.</summary>
    public bool ShowLayersPanel { get; set; } = true;
}
