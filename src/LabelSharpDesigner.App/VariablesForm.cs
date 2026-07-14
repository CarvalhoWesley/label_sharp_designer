using System.ComponentModel;
using System.Globalization;
using LabelSharpDesigner.Core.Document;
using LabelSharpDesigner.Core.Elements;

namespace LabelSharpDesigner.App;

/// <summary>
/// Manages a document's own <see cref="LabelDocument.Variables"/> — the one place in the editor
/// where a <c>{{ }}</c> variable is actually *declared* (name, type, default value, description),
/// independent of whichever elements happen to *reference* one by name (a <c>VariableElement</c>'s
/// bare <c>Expression</c>, or <c>{{ name }}</c> inside a <see cref="TextElement.Content"/>/
/// <see cref="BarcodeElement.Data"/>/<see cref="QrCodeElement.Data"/>).
///
/// <para>Referencing an undeclared name, or a declared one whose <c>DefaultValue</c> doesn't parse
/// for its own <c>Type</c>, throws during resolve — the "Pré-visualizar" tab has nowhere else to
/// show that failure than going blank (see <see cref="EditorForm"/>'s preview error label). Validating
/// every default value here, up front, is what prevents that: this dialog refuses to save a
/// <c>Number</c>/<c>Date</c>/<c>Boolean</c> variable whose default doesn't actually parse as one.</para>
/// </summary>
public sealed class VariablesForm : Form
{
    private readonly LabelDocument _document;
    private readonly BindingList<VariableRow> _rows;
    private readonly DataGridView _grid;

    public IReadOnlyList<LabelVariable>? Result { get; private set; }

    public VariablesForm(LabelDocument document)
    {
        _document = document;
        _rows = new BindingList<VariableRow>(document.Variables.Select(VariableRow.FromVariable).ToList());

        Text = "Variáveis da etiqueta";
        Width = 640;
        Height = 480;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(480, 320);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(8, 6, 8, 6),
        };
        var addButton = new Button { Text = "+ Nova variável", AutoSize = true, FlatStyle = FlatStyle.System };
        addButton.Click += (_, _) => AddRow();
        var removeButton = new Button { Text = "Remover", AutoSize = true, FlatStyle = FlatStyle.System };
        removeButton.Click += (_, _) => RemoveSelectedRow();
        toolbar.Controls.Add(addButton);
        toolbar.Controls.Add(removeButton);

        _grid = BuildGrid();

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
        var saveButton = new Button { Text = "Salvar", Width = 90, FlatStyle = FlatStyle.System };
        saveButton.Click += (_, _) => Save();
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(saveButton);

        var gridPanel = new Panel { Dock = DockStyle.Fill };
        gridPanel.Controls.Add(_grid);

        Controls.Add(gridPanel);
        Controls.Add(toolbar);
        Controls.Add(buttonPanel);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private DataGridView BuildGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoGenerateColumns = false,
            DataSource = _rows,
        };

        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Nome", DataPropertyName = "Name", FillWeight = 130 });
        grid.Columns.Add(new DataGridViewComboBoxColumn
        {
            Name = "Type",
            HeaderText = "Tipo",
            DataPropertyName = "Type",
            DataSource = Enum.GetValues<VariableValueType>(),
            FillWeight = 90,
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "DefaultValue", HeaderText = "Valor padrão", DataPropertyName = "DefaultValue", FillWeight = 110 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "Descrição", DataPropertyName = "Description", FillWeight = 150 });

        // Combo box columns need the same forced-commit trick as checkbox columns — without it,
        // changing "Tipo" and immediately hitting "Salvar" would validate against the previous type.
        grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (grid.IsCurrentCellDirty)
            {
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };

        return grid;
    }

    private void AddRow()
    {
        var existingNames = _rows.Select(r => r.Name).ToHashSet(StringComparer.Ordinal);
        var name = "nova_variavel";
        var suffix = 2;
        while (existingNames.Contains(name))
        {
            name = $"nova_variavel_{suffix++}";
        }

        _rows.Add(new VariableRow { Name = name, Type = VariableValueType.String, DefaultValue = string.Empty, Description = string.Empty });
    }

    private void RemoveSelectedRow()
    {
        if (_grid.CurrentRow?.DataBoundItem is not VariableRow row)
        {
            return;
        }

        if (IsReferenced(_document, row.Name))
        {
            var result = MessageBox.Show(
                this,
                $"A variável \"{row.Name}\" ainda é referenciada por algum elemento da etiqueta. " +
                "Removê-la vai fazer esse(s) elemento(s) falhar ao resolver (a Pré-visualização e a impressão vão mostrar um erro). Remover mesmo assim?",
                "Remover variável",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (result != DialogResult.Yes)
            {
                return;
            }
        }

        _rows.Remove(row);
    }

    /// <summary>Validates every row (unique non-empty name, default value parses for its own type)
    /// before producing <see cref="Result"/> — this is what keeps an invalid variable from ever
    /// reaching the resolve pipeline in the first place (see class doc).</summary>
    private void Save()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in _rows)
        {
            var name = row.Name.Trim();
            if (name.Length == 0)
            {
                MessageBox.Show(this, "Toda variável precisa de um nome.", "Variáveis da etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!names.Add(name))
            {
                MessageBox.Show(this, $"Já existe uma variável chamada \"{name}\".", "Variáveis da etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!string.IsNullOrEmpty(row.DefaultValue) && !TryParseForType(row.Type, row.DefaultValue))
            {
                MessageBox.Show(
                    this,
                    $"O valor padrão \"{row.DefaultValue}\" da variável \"{name}\" não é válido para o tipo {row.Type}.",
                    "Variáveis da etiqueta",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
        }

        Result = _rows.Select(row => new LabelVariable
        {
            Name = row.Name.Trim(),
            Type = row.Type,
            DefaultValue = string.IsNullOrEmpty(row.DefaultValue) ? null : row.DefaultValue,
            Description = string.IsNullOrEmpty(row.Description) ? null : row.Description,
        }).ToList();

        DialogResult = DialogResult.OK;
        Close();
    }

    private static bool TryParseForType(VariableValueType type, string value) => type switch
    {
        VariableValueType.Number => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
        VariableValueType.Date => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
        VariableValueType.Boolean => bool.TryParse(value, out _),
        _ => true,
    };

    /// <summary>Best-effort check for whether any element still points at <paramref name="name"/> —
    /// a <see cref="VariableElement"/>'s bare <c>Expression</c>, a <c>{{ name }}</c> placeholder inside
    /// free text/barcode/QR/image data, or a <see cref="DateElement"/>/<see cref="TimeElement"/> bound
    /// to it by <c>VariableName</c>. Only used to warn before deleting — a false negative (missed
    /// reference inside a more complex expression like <c>{{ preco * 2 }}</c>) just means no warning,
    /// never a blocked deletion.</summary>
    private static bool IsReferenced(LabelDocument document, string name) =>
        document.Elements.Any(element => ReferencesVariable(element, name));

    private static bool ReferencesVariable(LabelElement element, string name) => element switch
    {
        VariableElement v => v.Expression.Trim() == name,
        TextElement t => ContainsPlaceholder(t.Content, name),
        BarcodeElement b => ContainsPlaceholder(b.Data, name),
        QrCodeElement q => ContainsPlaceholder(q.Data, name),
        ImageElement i => ContainsPlaceholder(i.Source, name),
        DateElement d => d.VariableName == name,
        TimeElement t => t.VariableName == name,
        GroupElement g => g.Children.Any(child => ReferencesVariable(child, name)),
        _ => false,
    };

    private static bool ContainsPlaceholder(string text, string name)
    {
        var i = 0;
        while (true)
        {
            var start = text.IndexOf("{{", i, StringComparison.Ordinal);
            if (start < 0)
            {
                return false;
            }

            var end = text.IndexOf("}}", start + 2, StringComparison.Ordinal);
            if (end < 0)
            {
                return false;
            }

            if (text[(start + 2)..end].Trim() == name)
            {
                return true;
            }

            i = end + 2;
        }
    }

    private sealed class VariableRow
    {
        public required string Name { get; set; }
        public VariableValueType Type { get; set; }
        public string DefaultValue { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public static VariableRow FromVariable(LabelVariable variable) => new()
        {
            Name = variable.Name,
            Type = variable.Type,
            DefaultValue = variable.DefaultValue ?? string.Empty,
            Description = variable.Description ?? string.Empty,
        };
    }
}
