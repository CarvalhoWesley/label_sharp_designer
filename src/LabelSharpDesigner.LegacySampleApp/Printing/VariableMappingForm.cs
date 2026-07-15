using LabelSharpDesigner.Core.Document;

namespace LabelSharpDesigner.LegacySampleApp.Printing;

/// <summary>Lets the user bind each of the selected label's own <c>{{ }}</c> variables to one of
/// <see cref="Products.Product"/>'s fields (or leave it on the variable's own default) — one row per
/// variable, built dynamically from whatever the label document declares.</summary>
public sealed class VariableMappingForm : Form
{
    private static readonly string[] FieldOptions =
    {
        "Valor padrão da etiqueta",
        "Descrição do produto",
        "Preço do produto",
        "Código de barras do produto",
    };

    private readonly List<(LabelVariable Variable, ComboBox Combo)> _rows = new();

    public Dictionary<string, ProductFieldSource>? Result { get; private set; }

    public VariableMappingForm(LabelDocument document, IReadOnlyDictionary<string, ProductFieldSource> current)
    {
        Text = $"Vincular campos — {document.Name}";
        Width = 460;
        Height = Math.Max(260, Math.Min(640, 150 + document.Variables.Count * 34 + 90));
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var content = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12) };

        if (document.Variables.Count == 0)
        {
            content.Controls.Add(new Label
            {
                Text = "Esta etiqueta não declara nenhuma variável {{ }} para preencher.",
                Left = 0,
                Top = 0,
                Width = 400,
                Height = 40,
            });
        }
        else
        {
            var top = 0;
            content.Controls.Add(new Label { Text = "Variável da etiqueta", Left = 0, Top = top, Width = 200, Font = new Font(Font, FontStyle.Bold) });
            content.Controls.Add(new Label { Text = "Campo do produto", Left = 210, Top = top, Width = 200, Font = new Font(Font, FontStyle.Bold) });
            top += 26;

            foreach (var variable in document.Variables)
            {
                var label = new Label { Text = $"{variable.Name} ({variable.Type})", Left = 0, Top = top + 3, Width = 200, Height = 18 };
                var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Left = 210, Top = top, Width = 210 };
                combo.Items.AddRange(FieldOptions);
                combo.SelectedIndex = current.TryGetValue(variable.Name, out var source) ? (int)source : (int)ProductFieldSource.LabelDefault;

                content.Controls.Add(label);
                content.Controls.Add(combo);
                _rows.Add((variable, combo));

                top += 34;
            }
        }

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };
        var cancelButton = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Width = 90, FlatStyle = FlatStyle.System };
        var saveButton = new Button { Text = "Salvar", Width = 90, FlatStyle = FlatStyle.System };
        saveButton.Click += (_, _) => Save();
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(saveButton);

        Controls.Add(content);
        Controls.Add(buttonPanel);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private void Save()
    {
        Result = _rows.ToDictionary(row => row.Variable.Name, row => (ProductFieldSource)row.Combo.SelectedIndex, StringComparer.Ordinal);
        DialogResult = DialogResult.OK;
        Close();
    }
}
