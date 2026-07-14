using LabelSharpDesigner.Core.Document;

namespace LabelSharpDesigner.App;

/// <summary>The editor window's own layout preferences — side-panel widths and whether each is
/// currently collapsed — persisted across app runs so resizing/closing "Camadas" or the
/// properties/preview panel sticks the next time the editor opens. An app-level UI preference (see
/// <see cref="EditorLayoutSettingsStore"/>), not part of any <see cref="LabelDocument"/>: it's about
/// how you arrange the editor, not what's on the label.</summary>
public sealed class EditorLayoutSettings
{
    public int LayersPanelWidth { get; set; } = 240;
    public bool LayersPanelVisible { get; set; } = true;
    public int SidePanelWidth { get; set; } = 280;
    public bool SidePanelVisible { get; set; } = true;
}
