using LabelSharpDesignerCore.Core.Document;

namespace LabelSharpDesignerCore.LegacySampleApp.Labels;

/// <summary>Name + page size for a brand-new label — this app's own equivalent of the plugin's "nova
/// etiqueta" page-settings step, asked before the blank document is created and handed to the
/// editor.</summary>
public sealed class NewLabelForm : Form
{
    private readonly TextBox _nameBox;
    private readonly NumericUpDown _widthUpDown;
    private readonly NumericUpDown _heightUpDown;
    private readonly NumericUpDown _dpiUpDown;
    private readonly NumericUpDown _columnsUpDown;

    public (string Name, PageConfig Page)? Result { get; private set; }

    public NewLabelForm()
    {
        Text = "Nova etiqueta";
        Width = 380;
        Height = 340;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        var left = content.Padding.Left;
        var top = content.Padding.Top;

        Label AddLabel(string text)
        {
            var label = new Label { Text = text, Left = left, Top = top, Width = 320, Height = 18 };
            content.Controls.Add(label);
            top += 20;
            return label;
        }

        var defaultPage = LabelRepository.DefaultPage;

        AddLabel("Nome");
        _nameBox = new TextBox { Left = left, Top = top, Width = 320, Text = "Nova etiqueta" };
        content.Controls.Add(_nameBox);
        top += 34;

        AddLabel("Largura (mm)");
        _widthUpDown = new NumericUpDown { Left = left, Top = top, Width = 100, Minimum = 5, Maximum = 500, DecimalPlaces = 1, Value = (decimal)defaultPage.WidthMm };
        content.Controls.Add(_widthUpDown);
        top += 34;

        AddLabel("Altura (mm)");
        _heightUpDown = new NumericUpDown { Left = left, Top = top, Width = 100, Minimum = 5, Maximum = 500, DecimalPlaces = 1, Value = (decimal)defaultPage.HeightMm };
        content.Controls.Add(_heightUpDown);
        top += 34;

        AddLabel("DPI");
        _dpiUpDown = new NumericUpDown { Left = left, Top = top, Width = 100, Minimum = 72, Maximum = 1200, Value = defaultPage.Dpi };
        content.Controls.Add(_dpiUpDown);
        top += 34;

        AddLabel("Colunas (etiquetas por fileira)");
        _columnsUpDown = new NumericUpDown { Left = left, Top = top, Width = 100, Minimum = 1, Maximum = 99, Value = defaultPage.Columns };
        content.Controls.Add(_columnsUpDown);
        top += 34;

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };
        var cancelButton = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Width = 90, FlatStyle = FlatStyle.System };
        var createButton = new Button { Text = "Criar e editar", Width = 110, FlatStyle = FlatStyle.System };
        createButton.Click += (_, _) => TryCreate();
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(createButton);

        Controls.Add(content);
        Controls.Add(buttonPanel);
        AcceptButton = createButton;
        CancelButton = cancelButton;
    }

    private void TryCreate()
    {
        var name = _nameBox.Text.Trim();
        if (name.Length == 0)
        {
            MessageBox.Show(this, "Informe o nome da etiqueta.", "Nova etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var page = new PageConfig
        {
            WidthMm = (double)_widthUpDown.Value,
            HeightMm = (double)_heightUpDown.Value,
            Dpi = (int)_dpiUpDown.Value,
            Columns = (int)_columnsUpDown.Value,
        };

        Result = (name, page);
        DialogResult = DialogResult.OK;
        Close();
    }
}
