using LabelSharpDesignerCore.LegacySampleApp.Labels;
using LabelSharpDesignerCore.LegacySampleApp.Products;

namespace LabelSharpDesignerCore.LegacySampleApp;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // net48 has no ApplicationConfiguration.Initialize() (that's a net6.0-windows+ SDK template
        // helper) — this is the classic WinForms bootstrap it expands to.
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var productRepository = ProductRepository.Open();
        var labelRepository = LabelRepository.Open();

        Application.Run(new MainForm(productRepository, labelRepository));
    }
}
