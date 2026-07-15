namespace LabelSharpDesigner.LegacySampleApp.Printing;

/// <summary>Output format for a batch print job — this app's own equivalent of the plugin's internal
/// <c>PrintDialogForm.PrintFormat</c> (not referenceable here: it lives in <c>LabelSharpDesigner.App</c>,
/// which this project deliberately never references).</summary>
public enum PrintFormat
{
    Pdf,
    PplaNative,
    PplaRaster,
}
