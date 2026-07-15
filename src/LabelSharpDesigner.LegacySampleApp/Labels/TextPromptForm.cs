namespace LabelSharpDesigner.LegacySampleApp.Labels;

/// <summary>Small single-textbox prompt dialog — used for renaming a label (a plain metadata edit
/// this app makes directly, without needing the plugin's editor).</summary>
public sealed class TextPromptForm : Form
{
    private readonly TextBox _textBox;

    public string? Result { get; private set; }

    public TextPromptForm(string title, string label, string initialValue)
    {
        Text = title;
        Width = 380;
        Height = 160;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        content.Controls.Add(new Label { Text = label, Left = 0, Top = 0, Width = 320, Height = 18 });

        _textBox = new TextBox { Left = 0, Top = 22, Width = 320, Text = initialValue };
        content.Controls.Add(_textBox);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };
        var cancelButton = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Width = 90, FlatStyle = FlatStyle.System };
        var saveButton = new Button { Text = "OK", Width = 90, FlatStyle = FlatStyle.System };
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
        var value = _textBox.Text.Trim();
        if (value.Length == 0)
        {
            MessageBox.Show(this, "Informe um valor.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Result = value;
        DialogResult = DialogResult.OK;
        Close();
    }
}
