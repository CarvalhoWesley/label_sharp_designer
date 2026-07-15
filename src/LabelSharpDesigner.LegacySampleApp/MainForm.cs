using LabelSharpDesigner.LegacySampleApp.Labels;
using LabelSharpDesigner.LegacySampleApp.Printing;
using LabelSharpDesigner.LegacySampleApp.Products;

namespace LabelSharpDesigner.LegacySampleApp;

/// <summary>
/// Dashboard for this .NET Framework 4.x sample: products and labels are entirely owned/managed by
/// this application (its own repositories, its own list/CRUD screens) — the LabelSharpDesigner
/// plugin is only ever invoked for the visual editor itself (<see cref="LabelListForm"/>, via
/// <c>LegacyLauncher</c>), never for its own library/manager UI. This mirrors how a real .NET
/// Framework 4.x host application integrates the plugin (INTEGRATION.md, "Caminho B").
/// </summary>
public sealed class MainForm : Form
{
    private readonly ProductRepository _productRepository;
    private readonly LabelRepository _labelRepository;

    public MainForm(ProductRepository productRepository, LabelRepository labelRepository)
    {
        _productRepository = productRepository;
        _labelRepository = labelRepository;

        Text = "LabelSharpDesigner — Exemplo .NET Framework 4.x";
        Width = 480;
        Height = 360;
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

        var labelsButton = NewMenuButton("Etiquetas", "Catálogo próprio de etiquetas — o editor visual é o único pedaço do plugin usado aqui");
        labelsButton.Click += (_, _) => new LabelListForm(_labelRepository).ShowDialog(this);

        var printButton = NewMenuButton("Imprimir etiquetas", "Selecionar produtos, quantidades e imprimir");
        printButton.Click += (_, _) => new PrintProductsForm(_productRepository, _labelRepository).ShowDialog(this);

        var settingsButton = NewMenuButton("Configurações", "Localizar o LabelSharpDesigner.App.exe usado para editar etiquetas");
        settingsButton.Click += (_, _) => new EditorLauncherSettingsForm().ShowDialog(this);

        content.Controls.Add(title);
        content.Controls.Add(productsButton);
        content.Controls.Add(labelsButton);
        content.Controls.Add(printButton);
        content.Controls.Add(settingsButton);

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
        Width = 400,
        Height = 56,
        AutoSize = false,
        FlatStyle = FlatStyle.System,
        Margin = new Padding(0, 0, 0, 12),
        TextAlign = ContentAlignment.MiddleLeft,
    };
}
