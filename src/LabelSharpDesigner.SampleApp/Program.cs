using LabelSharpDesigner.App.Library;
using LabelSharpDesigner.SampleApp.Products;

namespace LabelSharpDesigner.SampleApp;

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
