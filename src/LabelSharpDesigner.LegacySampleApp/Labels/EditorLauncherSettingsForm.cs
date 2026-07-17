namespace LabelSharpDesigner.LegacySampleApp.Labels;

/// <summary>Lets the developer/admin configure how this app talks to the plugin's editor: where the
/// published <c>LabelSharpDesigner.App.exe</c> satellite lives (INTEGRATION.md §3.1), which element
/// kinds its "+ Adicionar" menu is allowed to offer — everything, by default, or a restricted subset
/// (e.g. only Texto/Código de barras/QR Code) for an integration that only ever needs those — and
/// whether its layers sidebar is offered at all. Persists via <see cref="EditorLauncherSettingsStore"/>.</summary>
public sealed class EditorLauncherSettingsForm : Form
{
    private readonly TextBox _pathBox;
    private readonly CheckedListBox _elementsList;
    private readonly CheckBox _showLayersPanelCheck;

    public EditorLauncherSettingsForm()
    {
        var settings = EditorLauncherSettingsStore.Load();

        Text = "Configurações — editor de etiquetas";
        Width = 560;
        Height = 460;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        var left = content.Padding.Left;
        var top = content.Padding.Top;

        content.Controls.Add(new Label
        {
            Text = "Caminho do LabelSharpDesigner.App.exe (publicado separadamente — INTEGRATION.md §3.1):",
            Left = left,
            Top = top,
            Width = 520,
            Height = 34,
        });

        _pathBox = new TextBox
        {
            Left = left,
            Top = top + 38,
            Width = 400,
            Text = settings.AppExecutablePath ?? string.Empty,
        };
        content.Controls.Add(_pathBox);

        var browseButton = new Button { Text = "Procurar...", Left = left + 408, Top = top + 36, Width = 100, FlatStyle = FlatStyle.System };
        browseButton.Click += (_, _) => Browse();
        content.Controls.Add(browseButton);

        var autoDetectButton = new Button { Text = "Detectar automaticamente", Left = left, Top = top + 70, Width = 220, FlatStyle = FlatStyle.System };
        autoDetectButton.Click += (_, _) => AutoDetect();
        content.Controls.Add(autoDetectButton);

        content.Controls.Add(new Label
        {
            Text = "Elementos que o editor pode oferecer em \"+ Adicionar\" (padrão: todos):",
            Left = left,
            Top = top + 112,
            Width = 520,
            Height = 18,
        });

        // Nothing configured yet (null/empty) means "unrestricted" — reflect that in the UI as
        // everything checked, matching what the editor actually does by default.
        var allowedNames = settings.AllowedElementKinds is { Count: > 0 }
            ? new HashSet<string>(settings.AllowedElementKinds, StringComparer.OrdinalIgnoreCase)
            : null;

        _elementsList = new CheckedListBox { Left = left, Top = top + 134, Width = 300, Height = 170, CheckOnClick = true };
        foreach (var (name, displayLabel) in ElementKindNames.All)
        {
            var isChecked = allowedNames is null || allowedNames.Contains(name);
            _elementsList.Items.Add(new ElementKindItem(name, displayLabel), isChecked);
        }

        content.Controls.Add(_elementsList);

        var selectAllButton = new Button { Text = "Selecionar todos", Left = left + 310, Top = top + 134, Width = 200, FlatStyle = FlatStyle.System };
        selectAllButton.Click += (_, _) => SetAllChecked(true);
        content.Controls.Add(selectAllButton);

        var selectNoneButton = new Button { Text = "Limpar seleção", Left = left + 310, Top = top + 168, Width = 200, FlatStyle = FlatStyle.System };
        selectNoneButton.Click += (_, _) => SetAllChecked(false);
        content.Controls.Add(selectNoneButton);

        _showLayersPanelCheck = new CheckBox
        {
            Text = "Exibir painel de camadas",
            Left = left,
            Top = top + 312,
            Width = 300,
            Checked = settings.ShowLayersPanel,
        };
        content.Controls.Add(_showLayersPanelCheck);

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

    private void SetAllChecked(bool isChecked)
    {
        for (var i = 0; i < _elementsList.Items.Count; i++)
        {
            _elementsList.SetItemChecked(i, isChecked);
        }
    }

    private void Browse()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Localizar LabelSharpDesigner.App.exe",
            Filter = "LabelSharpDesigner.App.exe|LabelSharpDesigner.App.exe|Executável (*.exe)|*.exe",
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _pathBox.Text = dialog.FileName;
        }
    }

    private void AutoDetect()
    {
        var detected = EditorLauncherLocator.TryAutoDetect();
        if (detected is null)
        {
            MessageBox.Show(this, "Não foi possível localizar automaticamente. Publique o LabelSharpDesigner.App (INTEGRATION.md §3.1) ou use \"Procurar...\".", "Configurações", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _pathBox.Text = detected;
    }

    private void TrySave()
    {
        var path = _pathBox.Text.Trim();
        if (path.Length == 0 || !File.Exists(path))
        {
            MessageBox.Show(this, "Informe um caminho válido para o LabelSharpDesigner.App.exe.", "Configurações", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var checkedNames = _elementsList.CheckedItems.Cast<ElementKindItem>().Select(item => item.Name).ToList();

        // All checked (the default state) is stored as "unrestricted" (null) rather than the full
        // explicit list — keeps a brand-new element kind added to a future plugin version available
        // automatically instead of silently excluded because it wasn't in the list saved back when
        // this dialog only knew about the kinds that existed at the time.
        var allowedElementKinds = checkedNames.Count == ElementKindNames.All.Count ? null : checkedNames;

        if (checkedNames.Count == 0)
        {
            MessageBox.Show(this, "Selecione ao menos um tipo de elemento (ou \"Selecionar todos\").", "Configurações", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        EditorLauncherSettingsStore.Save(new EditorLauncherSettings
        {
            AppExecutablePath = path,
            AllowedElementKinds = allowedElementKinds,
            ShowLayersPanel = _showLayersPanelCheck.Checked,
        });
        DialogResult = DialogResult.OK;
        Close();
    }

    private sealed record ElementKindItem(string Name, string DisplayLabel)
    {
        public override string ToString() => DisplayLabel;
    }
}
