namespace LabelSharpDesigner.LegacySampleApp.Printing;

/// <summary>The last "quantidade de colunas" the user actually printed with — an app-level
/// preference about the physical roll/printer setup being used, not about any one label document.
/// Null until the first successful print.</summary>
public sealed class PrintColumnsSettings
{
    public int? Columns { get; set; }
}
