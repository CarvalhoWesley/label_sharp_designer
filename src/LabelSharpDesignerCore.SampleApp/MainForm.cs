using LabelSharpDesignerCore.App;
using LabelSharpDesignerCore.App.Library;
using LabelSharpDesignerCore.SampleApp.Printing;
using LabelSharpDesignerCore.SampleApp.Products;

namespace LabelSharpDesignerCore.SampleApp;

/// <summary>Dashboard for this sample app: products, labels (reusing the LabelSharpDesignerCore plugin's
/// own <see cref="LibraryForm"/> directly), and batch label printing.</summary>
public sealed class MainForm : Form
{
    private readonly ProductRepository _productRepository;
    private readonly LibraryRepository _labelRepository;

    public MainForm(ProductRepository productRepository, LibraryRepository labelRepository)
    {
        _productRepository = productRepository;
        _labelRepository = labelRepository;

        Text = "LabelSharpDesignerCore — Exemplo de uso";
        Width = 480;
        Height = 320;
        StartPosition = FormStartPosition.CenterScreen;

        var content = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            Anchor = AnchorStyles.None,
            Padding = new Padding(24),
        };

        var title = new Label
        {
            Text = "O que você quer fazer?",
            Font = new Font(Font.FontFamily, 14f),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 16),
        };

        var productsButton = NewMenuButton("Produtos", "Cadastrar e listar produtos");
        productsButton.Click += (_, _) => new ProductListForm(_productRepository).ShowDialog(this);

        var labelsButton = NewMenuButton("Etiquetas", "Criar e listar etiquetas (LabelSharpDesignerCore)");
        labelsButton.Click += (_, _) => new LibraryForm(_labelRepository).ShowDialog(this);

        var printButton = NewMenuButton("Imprimir etiquetas", "Selecionar produtos, quantidades e imprimir");
        printButton.Click += (_, _) => new PrintProductsForm(_productRepository, _labelRepository).ShowDialog(this);

        content.Controls.Add(title);
        content.Controls.Add(productsButton);
        content.Controls.Add(labelsButton);
        content.Controls.Add(printButton);

        Controls.Add(content);
        Resize += (_, _) => CenterContent(content);
        content.SizeChanged += (_, _) => CenterContent(content);
    }

    private void CenterContent(Control content)
    {
        content.Left = Math.Max(0, (ClientSize.Width - content.Width) / 2);
        content.Top = Math.Max(0, (ClientSize.Height - content.Height) / 2);
    }

    private static Button NewMenuButton(string text, string description) => new()
    {
        Text = $"{text}\n{description}",
        Width = 360,
        Height = 56,
        AutoSize = false,
        FlatStyle = FlatStyle.System,
        Margin = new Padding(0, 0, 0, 12),
        TextAlign = ContentAlignment.MiddleLeft,
    };
}
