namespace LabelSharpDesignerCore.SampleApp.Printing;

/// <summary>The last "quantidade de colunas" the user actually printed with — an app-level
/// preference about the physical roll/printer setup being used, not about any one label document
/// (mirrors <c>PrintSettings</c> in the main App). Null until the first successful print, at which
/// point <see cref="PrintProductsForm"/> stops suggesting the selected label's own
/// <c>Page.Columns</c> and instead sticks to this persisted value across label choices and app runs.</summary>
public sealed class PrintColumnsSettings
{
    public int? Columns { get; set; }
}
