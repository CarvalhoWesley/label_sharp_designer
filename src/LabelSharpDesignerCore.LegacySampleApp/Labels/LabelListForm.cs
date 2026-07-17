using LabelSharpDesignerCore.Legacy.Bridge;

namespace LabelSharpDesignerCore.LegacySampleApp.Labels;

/// <summary>
/// This application's own "gerenciar etiquetas" screen: listing/creating/renaming/duplicating/deleting
/// labels is implemented here, against <see cref="LabelRepository"/> — none of it comes from the
/// plugin. The plugin is only ever invoked for one thing: drawing/editing the contents of a single
/// <c>.label</c> file, via <see cref="LegacyLauncher"/> starting the published
/// <c>LabelSharpDesignerCore.App.exe</c> as a satellite process and blocking until the user closes it
/// (INTEGRATION.md, "Caminho B") — exactly what a real .NET Framework 4.x host has to do, since it
/// cannot reference the plugin's own library/manager UI directly (different, incompatible runtime).
/// </summary>
public sealed class LabelListForm : Form
{
    private readonly LabelRepository _repository;
    private readonly DataGridView _grid;

    public LabelListForm(LabelRepository repository)
    {
        _repository = repository;

        Text = "Etiquetas";
        Width = 780;
        Height = 520;
        StartPosition = FormStartPosition.CenterScreen;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(8, 6, 8, 6),
        };

        var newButton = new Button { Text = "+ Nova etiqueta", AutoSize = true, FlatStyle = FlatStyle.System };
        newButton.Click += (_, _) => CreateLabel();
        var editButton = new Button { Text = "Editar (abre o editor do plugin)", AutoSize = true, FlatStyle = FlatStyle.System };
        editButton.Click += (_, _) => EditSelectedLabel();
        var renameButton = new Button { Text = "Renomear", AutoSize = true, FlatStyle = FlatStyle.System };
        renameButton.Click += (_, _) => RenameSelectedLabel();
        var duplicateButton = new Button { Text = "Duplicar", AutoSize = true, FlatStyle = FlatStyle.System };
        duplicateButton.Click += (_, _) => DuplicateSelectedLabel();
        var deleteButton = new Button { Text = "Excluir", AutoSize = true, FlatStyle = FlatStyle.System };
        deleteButton.Click += (_, _) => DeleteSelectedLabel();
        toolbar.Controls.Add(newButton);
        toolbar.Controls.Add(editButton);
        toolbar.Controls.Add(renameButton);
        toolbar.Controls.Add(duplicateButton);
        toolbar.Controls.Add(deleteButton);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoGenerateColumns = false,
        };
        _grid.CellDoubleClick += (_, _) => EditSelectedLabel();
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Nome", DataPropertyName = "Name", FillWeight = 220 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Size", HeaderText = "Tamanho", DataPropertyName = "Size", FillWeight = 120 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "VariableCount", HeaderText = "Variáveis", DataPropertyName = "VariableCount", FillWeight = 80 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "UpdatedAt", HeaderText = "Atualizado em", DataPropertyName = "UpdatedAt", FillWeight = 140 });

        Controls.Add(_grid);
        Controls.Add(toolbar);

        Refresh_();
    }

    private void Refresh_()
    {
        _grid.DataSource = _repository.List().Select(entry => new LabelRow(entry)).ToList();
    }

    private LabelEntry? SelectedEntry() =>
        (_grid.CurrentRow?.DataBoundItem as LabelRow)?.Entry;

    private void CreateLabel()
    {
        using var dialog = new NewLabelForm();
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result is not { } result)
        {
            return;
        }

        var entry = _repository.Create(result.Name, result.Page);
        Refresh_();

        // Immediately open the freshly created (blank) label in the plugin's editor — mirrors
        // INTEGRATION.md §3.3's "create a blank document, then call the editor" flow.
        OpenEditor(entry);
    }

    private void EditSelectedLabel()
    {
        if (SelectedEntry() is { } entry)
        {
            OpenEditor(entry);
        }
    }

    private void OpenEditor(LabelEntry entry)
    {
        var exePath = EditorLauncherLocator.Resolve();
        if (exePath is null)
        {
            MessageBox.Show(
                this,
                "O caminho do LabelSharpDesignerCore.App.exe (o editor do plugin) ainda não está configurado.",
                "Editor de etiquetas",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            using var settingsDialog = new EditorLauncherSettingsForm();
            if (settingsDialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            exePath = EditorLauncherLocator.Resolve();
            if (exePath is null)
            {
                return;
            }
        }

        LaunchResult result;
        try
        {
            var launcher = new LegacyLauncher(exePath);
            var editorSettings = EditorLauncherSettingsStore.Load();
            result = launcher.Launch(new LaunchRequest
            {
                FilePath = entry.FilePath,
                AllowedElementKinds = editorSettings.AllowedElementKinds,
                ShowLayersPanel = editorSettings.ShowLayersPanel,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Falha ao abrir o editor: {ex.Message}", "Editor de etiquetas", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        switch (result.Outcome)
        {
            case LaunchOutcome.Saved:
                Refresh_();
                break;
            case LaunchOutcome.Error:
                MessageBox.Show(this, "O editor não conseguiu abrir esta etiqueta (arquivo corrompido ou em formato inválido).", "Editor de etiquetas", MessageBoxButtons.OK, MessageBoxIcon.Error);
                break;
            case LaunchOutcome.Cancelled:
            default:
                break;
        }
    }

    private void RenameSelectedLabel()
    {
        var entry = SelectedEntry();
        if (entry is null)
        {
            return;
        }

        using var dialog = new TextPromptForm("Renomear etiqueta", "Novo nome", entry.Document.Name);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result is not { } newName)
        {
            return;
        }

        _repository.Rename(entry, newName);
        Refresh_();
    }

    private void DuplicateSelectedLabel()
    {
        if (SelectedEntry() is { } entry)
        {
            _repository.Duplicate(entry);
            Refresh_();
        }
    }

    private void DeleteSelectedLabel()
    {
        var entry = SelectedEntry();
        if (entry is null)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            $"Excluir \"{entry.Document.Name}\" permanentemente?",
            "Excluir etiqueta",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (result != DialogResult.Yes)
        {
            return;
        }

        _repository.Delete(entry);
        Refresh_();
    }

    /// <summary>Flattens <see cref="LabelEntry"/>'s nested <c>Document</c> fields onto get-only
    /// properties for grid binding — <see cref="DataGridView"/>'s <c>DataPropertyName</c> doesn't
    /// follow dotted/nested paths.</summary>
    private sealed class LabelRow
    {
        public LabelRow(LabelEntry entry)
        {
            Entry = entry;
        }

        public LabelEntry Entry { get; }

        public string Name => Entry.Document.Name;

        public string Size => $"{Entry.Document.Page.WidthMm:0.#} x {Entry.Document.Page.HeightMm:0.#} mm";

        public int VariableCount => Entry.Document.Variables.Count;

        public DateTimeOffset UpdatedAt => Entry.Document.Metadata.UpdatedAt;
    }
}
