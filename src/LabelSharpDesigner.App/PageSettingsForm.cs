using LabelSharpDesigner.Core.Document;

namespace LabelSharpDesigner.App;

/// <summary>Edits the document's <see cref="PageConfig"/> — size, DPI, orientation, margins and
/// columns — the settings the editor canvas itself has no other UI for.</summary>
public sealed class PageSettingsForm : Form
{
    private readonly NumericUpDown _widthMm;
    private readonly NumericUpDown _heightMm;
    private readonly NumericUpDown _dpi;
    private readonly ComboBox _orientation;
    private readonly NumericUpDown _marginTop;
    private readonly NumericUpDown _marginRight;
    private readonly NumericUpDown _marginBottom;
    private readonly NumericUpDown _marginLeft;
    private readonly NumericUpDown _columns;
    private readonly NumericUpDown _columnGapMm;

    public PageConfig Result { get; private set; }

    public PageSettingsForm(PageConfig page)
    {
        Result = page;

        Text = "Configurações da página";
        Width = 360;
        Height = 440;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), AutoScroll = true };
        var top = 0;

        _widthMm = AddNumeric(content, ref top, "Largura (mm)", (decimal)page.WidthMm, 1, 1000, 1);
        _heightMm = AddNumeric(content, ref top, "Altura (mm)", (decimal)page.HeightMm, 1, 1000, 1);
        _dpi = AddNumeric(content, ref top, "DPI", page.Dpi, 72, 1200, 0);

        AddLabel(content, ref top, "Orientação");
        _orientation = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Left = 0, Top = top, Width = 300 };
        _orientation.Items.AddRange(["Retrato", "Paisagem"]);
        _orientation.SelectedIndex = page.Orientation == PageOrientation.Landscape ? 1 : 0;
        content.Controls.Add(_orientation);
        top += 30;

        // Wired only after the initial SelectedIndex assignment above (which itself raises this event
        // once, from ComboBox's default -1) — swapping width/height every time orientation actually
        // changes (not just on construction) matches how orientation works in every other design tool:
        // flipping a page rotates it, so its width and height trade places, and flipping back restores
        // the original values.
        _orientation.SelectedIndexChanged += (_, _) => (_widthMm.Value, _heightMm.Value) = (_heightMm.Value, _widthMm.Value);

        AddLabel(content, ref top, "Margens (mm)");
        var marginsRow = top;
        _marginTop = AddNumericInline(content, 0, marginsRow, "Cima", (decimal)page.Margins.Top);
        _marginRight = AddNumericInline(content, 75, marginsRow, "Direita", (decimal)page.Margins.Right);
        _marginBottom = AddNumericInline(content, 150, marginsRow, "Baixo", (decimal)page.Margins.Bottom);
        _marginLeft = AddNumericInline(content, 225, marginsRow, "Esquerda", (decimal)page.Margins.Left);
        top += 46;

        _columns = AddNumeric(content, ref top, "Colunas", page.Columns, 1, 20, 0);
        _columnGapMm = AddNumeric(content, ref top, "Espaço entre colunas (mm)", (decimal)page.ColumnGapMm, 0, 100, 1);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };
        // FlatStyle.System works around a .NET 9 dark-mode bug where the default FlatStyle.Standard
        // renders no button text.
        var cancelButton = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Width = 90, FlatStyle = FlatStyle.System };
        var okButton = new Button { Text = "OK", Width = 90, FlatStyle = FlatStyle.System };
        okButton.Click += (_, _) => Apply();
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(okButton);

        Controls.Add(content);
        Controls.Add(buttonPanel);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private static void AddLabel(Control parent, ref int top, string text)
    {
        parent.Controls.Add(new Label { Text = text, Left = 0, Top = top, Width = 300, Height = 18 });
        top += 20;
    }

    private static NumericUpDown AddNumeric(Control parent, ref int top, string label, decimal value, decimal min, decimal max, int decimals)
    {
        AddLabel(parent, ref top, label);
        var field = new NumericUpDown
        {
            Left = 0,
            Top = top,
            Width = 300,
            Minimum = min,
            Maximum = max,
            DecimalPlaces = decimals,
            Increment = decimals > 0 ? 0.5m : 1m,
            Value = Math.Clamp(value, min, max),
        };
        parent.Controls.Add(field);
        top += 34;
        return field;
    }

    private static NumericUpDown AddNumericInline(Control parent, int left, int top, string label, decimal value)
    {
        parent.Controls.Add(new Label { Text = label, Left = left, Top = top, Width = 70, Height = 16 });
        var field = new NumericUpDown
        {
            Left = left,
            Top = top + 18,
            Width = 65,
            Minimum = 0,
            Maximum = 100,
            DecimalPlaces = 1,
            Increment = 0.5m,
            Value = Math.Clamp(value, 0, 100),
        };
        parent.Controls.Add(field);
        return field;
    }

    private void Apply()
    {
        Result = new PageConfig
        {
            WidthMm = (double)_widthMm.Value,
            HeightMm = (double)_heightMm.Value,
            DisplayUnit = Result.DisplayUnit,
            Dpi = (int)_dpi.Value,
            Orientation = _orientation.SelectedIndex == 1 ? PageOrientation.Landscape : PageOrientation.Portrait,
            Margins = new PageMargins((double)_marginTop.Value, (double)_marginRight.Value, (double)_marginBottom.Value, (double)_marginLeft.Value),
            Columns = (int)_columns.Value,
            ColumnGapMm = (double)_columnGapMm.Value,
        };

        DialogResult = DialogResult.OK;
        Close();
    }
}
