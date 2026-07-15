namespace LabelSharpDesigner.LegacySampleApp.Labels;

/// <summary>Lets the user point this app at the published <c>LabelSharpDesigner.App.exe</c> satellite
/// — the one-time admin setup step a real .NET Framework 4.x deployment would do once and never touch
/// again (INTEGRATION.md §3.1). Persists via <see cref="EditorLauncherSettingsStore"/>.</summary>
public sealed class EditorLauncherSettingsForm : Form
{
    private readonly TextBox _pathBox;

    public EditorLauncherSettingsForm()
    {
        Text = "Configurações — editor de etiquetas";
        Width = 560;
        Height = 220;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };

        content.Controls.Add(new Label
        {
            Text = "Caminho do LabelSharpDesigner.App.exe (publicado separadamente — INTEGRATION.md §3.1):",
            Left = 0,
            Top = 0,
            Width = 520,
            Height = 34,
        });

        _pathBox = new TextBox
        {
            Left = 0,
            Top = 38,
            Width = 400,
            Text = EditorLauncherSettingsStore.Load().AppExecutablePath ?? string.Empty,
        };
        content.Controls.Add(_pathBox);

        var browseButton = new Button { Text = "Procurar...", Left = 408, Top = 36, Width = 100, FlatStyle = FlatStyle.System };
        browseButton.Click += (_, _) => Browse();
        content.Controls.Add(browseButton);

        var autoDetectButton = new Button { Text = "Detectar automaticamente", Left = 0, Top = 70, Width = 220, FlatStyle = FlatStyle.System };
        autoDetectButton.Click += (_, _) => AutoDetect();
        content.Controls.Add(autoDetectButton);

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

        EditorLauncherSettingsStore.Save(new EditorLauncherSettings { AppExecutablePath = path });
        DialogResult = DialogResult.OK;
        Close();
    }
}
