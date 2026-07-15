namespace LabelSharpDesigner.LegacySampleApp.Products;

/// <summary>Create/edit dialog for a single <see cref="Product"/> — <see cref="Result"/> is set only
/// when the user confirms with valid data.</summary>
public sealed class ProductEditForm : Form
{
    private readonly TextBox _descriptionBox;
    private readonly NumericUpDown _priceUpDown;
    private readonly TextBox _barcodeBox;

    public (string Description, decimal Price, string Barcode)? Result { get; private set; }

    public ProductEditForm(Product? existing = null)
    {
        Text = existing is null ? "Novo produto" : "Editar produto";
        Width = 380;
        Height = 260;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };

        var top = 0;

        Label AddLabel(string text)
        {
            var label = new Label { Text = text, Left = 0, Top = top, Width = 320, Height = 18 };
            content.Controls.Add(label);
            top += 20;
            return label;
        }

        AddLabel("Descrição");
        _descriptionBox = new TextBox { Left = 0, Top = top, Width = 320, Text = existing?.Description ?? string.Empty };
        content.Controls.Add(_descriptionBox);
        top += 34;

        AddLabel("Preço");
        _priceUpDown = new NumericUpDown
        {
            Left = 0,
            Top = top,
            Width = 140,
            Minimum = 0,
            Maximum = 999_999,
            DecimalPlaces = 2,
            Increment = 0.1m,
            Value = existing?.Price ?? 0m,
        };
        content.Controls.Add(_priceUpDown);
        top += 34;

        AddLabel("Código de barras");
        _barcodeBox = new TextBox { Left = 0, Top = top, Width = 320, Text = existing?.Barcode ?? string.Empty };
        content.Controls.Add(_barcodeBox);
        top += 34;

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };
        var cancelButton = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Width = 90, FlatStyle = FlatStyle.System };
        var saveButton = new Button { Text = "Salvar", Width = 90, FlatStyle = FlatStyle.System };
        saveButton.Click += (_, _) => TrySave();
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(saveButton);

        Controls.Add(content);
        Controls.Add(buttonPanel);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private void TrySave()
    {
        var description = _descriptionBox.Text.Trim();
        var barcode = _barcodeBox.Text.Trim();

        if (description.Length == 0)
        {
            MessageBox.Show(this, "Informe a descrição do produto.", "Novo produto", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (barcode.Length == 0)
        {
            MessageBox.Show(this, "Informe o código de barras do produto.", "Novo produto", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Result = (description, _priceUpDown.Value, barcode);
        DialogResult = DialogResult.OK;
        Close();
    }
}
