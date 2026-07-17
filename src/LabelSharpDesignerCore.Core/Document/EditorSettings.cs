namespace LabelSharpDesignerCore.Core.Document;

/// <summary>
/// Editor view/interaction preferences (grid, snap, alignment guides) that belong to the label
/// itself rather than to a single editing session — reopening a saved <see cref="LabelDocument"/>
/// restores the same grid size/visibility and snapping behavior it was last edited with, instead of
/// always resetting to hardcoded defaults. Deliberately excluded from the layout engine's resolved
/// output: export/print output should never depend on how the label happened to look while being
/// edited.
/// </summary>
public sealed record EditorSettings
{
    public static readonly EditorSettings Default = new();

    public double GridSizeMm { get; init; } = 0.1;
    public bool ShowGrid { get; init; } = true;
    public bool SnapToGridEnabled { get; init; } = true;
    public bool AlignmentGuidesEnabled { get; init; } = true;
}
