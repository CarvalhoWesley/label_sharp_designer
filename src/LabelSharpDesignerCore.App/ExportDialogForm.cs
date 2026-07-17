using System.Globalization;
using LabelSharpDesignerCore.Core.Document;
using LabelSharpDesignerCore.Core.Layout;
using LabelSharpDesignerCore.Layout;
using LabelSharpDesignerCore.Rendering.Pdf;
using LabelSharpDesignerCore.Serialization;

namespace LabelSharpDesignerCore.App;

/// <summary>
/// Renders <see cref="LabelDocument"/> into a chosen output format (the editable <c>.label</c>
/// project itself, a vectorial PDF, or a PNG bitmap) resolved with sample data the user can
/// override for the document's <see cref="LabelVariable"/>s, and saves the bytes to a location the
/// user picks. Mirrors the original Flutter <c>export_dialog.dart</c>, minus the PPLA printer-command
/// format (added once Phase 8 brings <c>Rendering.ArgoxPpla</c>).
/// </summary>
public sealed class ExportDialogForm : Form
{
    private enum ExportFormat
    {
        Label,
        Pdf,
        Png,
    }

    private readonly LabelDocument _document;
    private readonly ComboBox _formatCombo;
    private readonly Panel _leftContent;
    private readonly RenderPreviewControl _preview;
    private readonly Label _placeholderLabel;
    private readonly Dictionary<string, TextBox> _sampleDataFields = new(StringComparer.Ordinal);

    private ComboBox? _pngScaleCombo;
    private Panel? _sampleDataSection;
    private int _top;

    public ExportDialogForm(LabelDocument document)
    {
        _document = document;

        Text = "Exportar etiqueta";
        Width = 820;
        Height = 560;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };
        // FlatStyle.System: FlatStyle.Standard (the default) renders no button text under .NET 9's
        // experimental dark mode — Flat/Popup/System all render correctly, System looks most native.
        var cancelButton = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Width = 90, FlatStyle = FlatStyle.System };
        var exportButton = new Button { Text = "Exportar...", Width = 110, FlatStyle = FlatStyle.System };
        exportButton.Click += (_, _) => Export();
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(exportButton);

        var leftPanel = new Panel { Dock = DockStyle.Left, Width = 340 };
        _leftContent = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12) };
        leftPanel.Controls.Add(_leftContent);
        _top = _leftContent.Padding.Top;

        _preview = new RenderPreviewControl { Dock = DockStyle.Fill };
        _placeholderLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(Font.FontFamily, 12f),
            ForeColor = SystemColors.GrayText,
            Text = "Salva o documento editável para reabrir aqui.",
            Visible = false,
        };

        var rightPanel = new Panel { Dock = DockStyle.Fill };
        rightPanel.Controls.Add(_preview);
        rightPanel.Controls.Add(_placeholderLabel);

        Controls.Add(rightPanel);
        Controls.Add(leftPanel);
        Controls.Add(buttonPanel);

        AddLabel("Formato");
        _formatCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300, Left = _leftContent.Padding.Left, Top = _top };
        _formatCombo.Items.AddRange(["Projeto (.label)", "PDF vetorial", "Imagem (PNG)"]);
        _formatCombo.SelectedIndexChanged += (_, _) => RebuildForFormat();
        _leftContent.Controls.Add(_formatCombo);
        _top += 30;

        BuildSampleDataSection();

        _formatCombo.SelectedIndex = 1; // PDF by default
    }

    private void AddLabel(string text)
    {
        var label = new Label { Text = text, Left = _leftContent.Padding.Left, Top = _top, Width = 300, Height = 18 };
        _leftContent.Controls.Add(label);
        _top += 20;
    }

    private void BuildSampleDataSection()
    {
        if (_document.Variables.Count == 0)
        {
            return;
        }

        AddLabel("Dados de amostra");
        var sectionTop = _top;
        var height = _document.Variables.Count * 46;
        _sampleDataSection = new Panel { Left = _leftContent.Padding.Left, Top = sectionTop, Width = 300, Height = height };

        var rowTop = 0;
        foreach (var variable in _document.Variables)
        {
            var nameLabel = new Label { Text = $"{variable.Name} ({variable.Type})", Left = 0, Top = rowTop, Width = 300, Height = 16 };
            var field = new TextBox { Text = variable.DefaultValue ?? string.Empty, Left = 0, Top = rowTop + 18, Width = 300 };
            field.TextChanged += (_, _) => UpdatePreview();
            _sampleDataFields[variable.Name] = field;

            _sampleDataSection.Controls.Add(nameLabel);
            _sampleDataSection.Controls.Add(field);
            rowTop += 46;
        }

        _leftContent.Controls.Add(_sampleDataSection);
        _top += height + 10;
    }

    private void RebuildForFormat()
    {
        _pngScaleCombo?.Dispose();
        _pngScaleCombo = null;

        var format = SelectedFormat();

        if (format == ExportFormat.Png)
        {
            AddLabel("Resolução");
            _pngScaleCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120, Left = _leftContent.Padding.Left, Top = _top };
            _pngScaleCombo.Items.AddRange(["1x", "2x", "3x"]);
            _pngScaleCombo.SelectedIndex = 1;
            _pngScaleCombo.SelectedIndexChanged += (_, _) => UpdatePreview();
            _leftContent.Controls.Add(_pngScaleCombo);
        }

        if (_sampleDataSection is not null)
        {
            _sampleDataSection.Visible = format != ExportFormat.Label;
        }

        _placeholderLabel.Visible = format == ExportFormat.Label;
        _preview.Visible = format != ExportFormat.Label;

        UpdatePreview();
    }

    private ExportFormat SelectedFormat() => (ExportFormat)_formatCombo.SelectedIndex;

    private void UpdatePreview()
    {
        if (SelectedFormat() == ExportFormat.Label)
        {
            return;
        }

        try
        {
            _preview.Document = Resolve();
        }
        catch
        {
            _preview.Document = null;
        }

        _preview.Invalidate();
    }

    private ResolvedDocument Resolve()
    {
        var options = new LayoutOptions { SampleData = BuildSampleData() };
        return new LayoutEngine().Resolve(_document, options);
    }

    private IReadOnlyDictionary<string, object?> BuildSampleData()
    {
        var data = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var variable in _document.Variables)
        {
            if (!_sampleDataFields.TryGetValue(variable.Name, out var field))
            {
                continue;
            }

            data[variable.Name] = ParseSampleValue(variable.Type, field.Text);
        }

        return data;
    }

    private static object? ParseSampleValue(VariableValueType type, string text) => type switch
    {
        VariableValueType.Number => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : text,
        VariableValueType.Date => DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : text,
        VariableValueType.Boolean => bool.TryParse(text, out var boolean) ? boolean : text,
        _ => text,
    };

    private void Export()
    {
        var format = SelectedFormat();
        byte[] bytes;
        string extension;
        string filter;

        try
        {
            switch (format)
            {
                case ExportFormat.Label:
                    bytes = System.Text.Encoding.UTF8.GetBytes(LabelDocumentCodec.Save(_document));
                    extension = "label";
                    filter = "Projeto LabelSharpDesignerCore (*.label)|*.label";
                    break;
                case ExportFormat.Pdf:
                    bytes = PdfExporter.Export(Resolve());
                    extension = "pdf";
                    filter = "PDF (*.pdf)|*.pdf";
                    break;
                case ExportFormat.Png:
                    var scaleIndex = (_pngScaleCombo?.SelectedIndex ?? 1) + 1;
                    bytes = Rendering.Png.PngExporter.Export(Resolve(), (Rendering.Png.PngScale)scaleIndex);
                    extension = "png";
                    filter = "Imagem PNG (*.png)|*.png";
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported export format '{format}'.");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Falha ao exportar: {ex.Message}", "Exportar etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Exportar etiqueta",
            FileName = $"{_document.Name}.{extension}",
            Filter = filter,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        File.WriteAllBytes(dialog.FileName, bytes);
        MessageBox.Show(this, $"Exportado em {dialog.FileName} ({bytes.Length} bytes)", "Exportar etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
        Close();
    }
}
