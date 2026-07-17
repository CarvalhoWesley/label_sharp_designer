using LabelSharpDesignerCore.App.Library;
using LabelSharpDesignerCore.SampleApp.Products;

namespace LabelSharpDesignerCore.SampleApp;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var productRepository = ProductRepository.Open();
        var labelRepository = LibraryRepository.Open();

        Application.Run(new MainForm(productRepository, labelRepository));
    }
}
