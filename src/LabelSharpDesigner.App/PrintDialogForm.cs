using System.Globalization;
using LabelSharpDesigner.Core.Document;
using LabelSharpDesigner.Core.Layout;
using LabelSharpDesigner.Layout;
using LabelSharpDesigner.PrintTransport.Windows;
using LabelSharpDesigner.Rendering.ArgoxPpla;
using LabelSharpDesigner.Rendering.Pdf;

namespace LabelSharpDesigner.App;

/// <summary>
/// Sends <see cref="LabelDocument"/> to a physical printer: PDF via the system driver (works with
/// any installed printer), or PPLA — native commands or a whole-label raster — for Argox thermal
/// printers. Mirrors <see cref="ExportDialogForm"/>'s sample-data-resolution/preview shape, but
/// drives an <see cref="IPrintTransport"/> instead of writing to a chosen file.
///
/// <para>There is exactly one label-count control, "Cópias" (or "Quantidade de etiquetas" once
/// <see cref="_isBatchMode"/>) — never a second multiplier layered on top. On a single-column label
/// it's a literal printer/driver repeat count (one resolved document, sent N times). On a
/// multi-column roll (<c>Page.Columns &gt; 1</c>) it instead *is* the total label count: that many
/// records get tiled across <c>Page.Columns</c> by <see cref="LayoutEngine.ResolveBatch"/> into
/// physical rows, each sent to the printer exactly once — so "2 colunas" + "2 cópias" prints exactly
/// 2 labels side by side, not 2 rows of 2. "Um valor diferente por etiqueta" additionally lets each
/// of those labels carry its own data instead of all being identical — see
/// <see cref="_perLabelCheckBox"/>. Ported from <c>apps/label_studio/lib/src/print/print_dialog.dart</c>'s
/// <c>_print</c>/<c>_copiesField</c>, which this mirrors control-for-control.</para>
/// </summary>
public sealed class PrintDialogForm : Form
{
    public enum PrintFormat
    {
        Pdf,
        PplaNative,
        PplaRaster,
    }

    private const int FormatOptionsHeight = 320;
    private const int RecordsAreaHeight = 260;

    private readonly LabelDocument _document;
    private readonly PrintSettings _lastSettings;

    /// <summary>Page.Columns &gt; 1 means this label is meant to print several-per-row on a
    /// multi-lane roll — that changes what the single quantity control means (see class doc) and
    /// unlocks "Um valor diferente por etiqueta" when the document also declares variables.</summary>
    private readonly bool _isBatchMode;

    private readonly ComboBox _printerCombo;
    private readonly ComboBox _formatCombo;
    private readonly Panel _formatOptionsPanel;
    private readonly Panel _leftContent;
    private readonly RenderPreviewControl _preview;
    private readonly Dictionary<string, TextBox> _sampleDataFields = new(StringComparer.Ordinal);

    private readonly Label _quantityLabel;
    private readonly NumericUpDown _quantityUpDown;
    private readonly CheckBox? _perLabelCheckBox;
    private readonly Panel _recordsAreaPanel;

    private NumericUpDown? _darknessUpDown;
    private ComboBox? _transferTypeCombo;
    private NumericUpDown? _offsetXUpDown;
    private NumericUpDown? _offsetYUpDown;
    private NumericUpDown? _feedOffsetUpDown;
    private CheckBox? _fullResolutionCheck;
    private CheckBox? _mirrorHorizontalCheck;
    private CheckBox? _reverseRowOrderCheck;
    private DataGridView? _recordsGrid;
    private int _top;

    public PrintDialogForm(LabelDocument document)
    {
        _document = document;
        _lastSettings = PrintSettingsStore.Load();
        _isBatchMode = document.Page.Columns > 1;

        Text = "Imprimir etiqueta";
        Width = 820;
        Height = 600;
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
        // FlatStyle.System works around a .NET 9 dark-mode bug where the default FlatStyle.Standard
        // renders no button text.
        var cancelButton = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Width = 90, FlatStyle = FlatStyle.System };
        var printButton = new Button { Text = "Imprimir", Width = 110, FlatStyle = FlatStyle.System };
        printButton.Click += (_, _) => PrintNow();
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(printButton);

        var leftPanel = new Panel { Dock = DockStyle.Left, Width = 340 };
        _leftContent = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12) };
        leftPanel.Controls.Add(_leftContent);
        _top = _leftContent.Padding.Top;

        _preview = new RenderPreviewControl { Dock = DockStyle.Fill };
        var rightPanel = new Panel { Dock = DockStyle.Fill };
        rightPanel.Controls.Add(_preview);

        Controls.Add(rightPanel);
        Controls.Add(leftPanel);
        Controls.Add(buttonPanel);

        AddLabel("Impressora");
        _printerCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300, Left = _leftContent.Padding.Left, Top = _top };
        _printerCombo.Items.Add("(Impressora padrão do sistema)");
        foreach (var printer in new WindowsPrinterDiscovery().ListAvailable())
        {
            _printerCombo.Items.Add(printer);
        }

        // Restore the last-used printer by name if it's still among the ones currently installed;
        // otherwise fall back to the system default rather than silently picking an arbitrary one.
        var savedPrinterIndex = _lastSettings.PrinterName is { } savedPrinter
            ? _printerCombo.Items.IndexOf(savedPrinter)
            : -1;
        _printerCombo.SelectedIndex = savedPrinterIndex >= 0 ? savedPrinterIndex : 0;
        _leftContent.Controls.Add(_printerCombo);
        _top += 30;

        AddLabel("Formato");
        _formatCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300, Left = _leftContent.Padding.Left, Top = _top };
        _formatCombo.Items.AddRange(["PDF (via driver, qualquer impressora)", "PPLA nativo (Argox)", "PPLA raster (Argox — texto/imagem/QR)"]);
        _formatCombo.SelectedIndexChanged += (_, _) => RebuildForFormat();
        _leftContent.Controls.Add(_formatCombo);
        _top += 30;

        // Fixed-size placeholder for whichever fields the selected format needs — sized generously
        // enough for the largest case (PPLA raster: darkness/copies/transfer type + 3 checkboxes) so
        // its position never shifts when the format changes, keeping the sample-data section below it
        // at a stable Top regardless of how many format-specific controls are currently shown.
        _formatOptionsPanel = new Panel { Left = _leftContent.Padding.Left, Top = _top, Width = 300, Height = FormatOptionsHeight };
        _leftContent.Controls.Add(_formatOptionsPanel);
        _top += FormatOptionsHeight + 10;

        // The single label-count control — always present, whatever the format. Seeded from the last
        // print's value regardless of mode; the label/meaning changes with _isBatchMode (see class doc).
        _quantityLabel = AddLabel(_isBatchMode ? "Quantidade de etiquetas" : "Cópias");
        _quantityUpDown = new NumericUpDown
        {
            Minimum = 1,
            Maximum = _isBatchMode ? 999 : 99,
            Value = Math.Clamp(_lastSettings.Quantity, 1, _isBatchMode ? 999 : 99),
            Width = 100,
            Left = _leftContent.Padding.Left,
            Top = _top,
        };
        _quantityUpDown.ValueChanged += (_, _) => UpdatePreview();
        _leftContent.Controls.Add(_quantityUpDown);
        _top += 34;

        if (_isBatchMode && _document.Variables.Count > 0)
        {
            _perLabelCheckBox = new CheckBox
            {
                Text = "Um valor diferente por etiqueta",
                Left = _leftContent.Padding.Left,
                Top = _top,
                Width = 300,
            };
            _perLabelCheckBox.CheckedChanged += (_, _) =>
            {
                RebuildRecordsArea();
                UpdatePreview();
            };
            _leftContent.Controls.Add(_perLabelCheckBox);
            _top += 26;
        }

        // Fixed-size placeholder, same reserved-space trick as _formatOptionsPanel below: holds either
        // the shared "Dados de amostra" fields or (per-label mode) the records grid, rebuilt in place
        // by RebuildRecordsArea whenever _perLabelCheckBox flips, without reflowing anything above it.
        _recordsAreaPanel = new Panel { Left = _leftContent.Padding.Left, Top = _top, Width = 300, Height = RecordsAreaHeight };
        _leftContent.Controls.Add(_recordsAreaPanel);
        _top += RecordsAreaHeight + 10;
        RebuildRecordsArea();

        // Clamp defensively — a hand-edited or stale settings file could carry an out-of-range value.
        _formatCombo.SelectedIndex = Math.Clamp((int)_lastSettings.Format, 0, _formatCombo.Items.Count - 1);
    }

    private Label AddLabel(string text)
    {
        var label = new Label { Text = text, Left = _leftContent.Padding.Left, Top = _top, Width = 300, Height = 18 };
        _leftContent.Controls.Add(label);
        _top += 20;
        return label;
    }

    /// <summary>Shows exactly one of: the per-label records grid ("Um valor diferente por etiqueta"
    /// checked), or the shared "Dados de amostra" fields (unchecked, or single-column mode) — and
    /// hides the quantity control whenever the grid's own row count already is the label count (mirrors
    /// <c>_optionsFor</c>'s <c>showCopies</c> in the Flutter original: two visible quantity controls
    /// at once would be a second, conflicting source of truth for the same number).</summary>
    private void RebuildRecordsArea()
    {
        foreach (Control control in _recordsAreaPanel.Controls.Cast<Control>().ToList())
        {
            control.Dispose();
        }

        _recordsAreaPanel.Controls.Clear();
        _sampleDataFields.Clear();
        _recordsGrid = null;

        var showGrid = _isBatchMode && (_perLabelCheckBox?.Checked ?? false);
        _quantityUpDown.Visible = !showGrid;
        _quantityLabel.Visible = !showGrid;

        if (showGrid)
        {
            BuildRecordsGrid();
        }
        else if (_document.Variables.Count > 0)
        {
            BuildSampleDataFields();
        }
    }

    private void BuildSampleDataFields()
    {
        var localTop = 0;
        var header = new Label { Text = "Dados de amostra", Left = 0, Top = localTop, Width = 300, Height = 18 };
        _recordsAreaPanel.Controls.Add(header);
        localTop += 20;

        foreach (var variable in _document.Variables)
        {
            var nameLabel = new Label { Text = $"{variable.Name} ({variable.Type})", Left = 0, Top = localTop, Width = 300, Height = 16 };
            var field = new TextBox { Text = variable.DefaultValue ?? string.Empty, Left = 0, Top = localTop + 18, Width = 300 };
            field.TextChanged += (_, _) => UpdatePreview();
            _sampleDataFields[variable.Name] = field;

            _recordsAreaPanel.Controls.Add(nameLabel);
            _recordsAreaPanel.Controls.Add(field);
            localTop += 46;
        }
    }

    /// <summary>One row per physical label — the row count itself is the label count in this mode, no
    /// separate quantity control shown alongside it (see <see cref="RebuildRecordsArea"/>).</summary>
    private void BuildRecordsGrid()
    {
        var header = new Label { Text = "Registros (um por etiqueta)", Left = 0, Top = 0, Width = 300, Height = 18 };
        _recordsAreaPanel.Controls.Add(header);

        var grid = new DataGridView
        {
            Left = 0,
            Top = 22,
            Width = 300,
            Height = RecordsAreaHeight - 22,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            RowHeadersVisible = false,
            AutoGenerateColumns = false,
        };

        foreach (var variable in _document.Variables)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = variable.Name,
                HeaderText = $"{variable.Name} ({variable.Type})",
                FillWeight = 100,
            });
        }

        // Seed one row from the variables' own defaults so the dialog isn't blank/empty-record on
        // first open — same starting point BuildSampleDataFields gives the shared-data case.
        grid.Rows.Add(_document.Variables.Select(v => (object)(v.DefaultValue ?? string.Empty)).ToArray());

        grid.CellValueChanged += (_, _) => UpdatePreview();
        grid.RowsAdded += (_, _) => UpdatePreview();
        grid.UserDeletedRow += (_, _) => UpdatePreview();

        _recordsAreaPanel.Controls.Add(grid);
        _recordsGrid = grid;
    }

    /// <summary>One dictionary per record, keyed by variable name — same shape <see cref="LayoutOptions.SampleData"/>
    /// expects. In per-label mode, one entry per grid row; otherwise the shared sample data repeated
    /// <see cref="_quantityUpDown"/> times, since every label in that mode is identical.</summary>
    private IReadOnlyList<IReadOnlyDictionary<string, object?>> BuildRecords()
    {
        if (_recordsGrid is { } grid)
        {
            var records = new List<IReadOnlyDictionary<string, object?>>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                var record = new Dictionary<string, object?>(StringComparer.Ordinal);
                for (var i = 0; i < _document.Variables.Count; i++)
                {
                    var variable = _document.Variables[i];
                    var text = row.Cells[i].Value?.ToString() ?? string.Empty;
                    record[variable.Name] = ParseSampleValue(variable.Type, text);
                }

                records.Add(record);
            }

            return records;
        }

        // Every label identical — the shared "Dados de amostra" values (or an empty set if the
        // document declares no variables) repeated once per requested label.
        var sample = BuildSampleData();
        return Enumerable.Repeat(sample, (int)_quantityUpDown.Value).ToList();
    }

    private void RebuildForFormat()
    {
        foreach (Control control in _formatOptionsPanel.Controls.Cast<Control>().ToList())
        {
            control.Dispose();
        }

        _formatOptionsPanel.Controls.Clear();
        _darknessUpDown = null;
        _transferTypeCombo = null;
        _offsetXUpDown = null;
        _offsetYUpDown = null;
        _feedOffsetUpDown = null;
        _fullResolutionCheck = null;
        _mirrorHorizontalCheck = null;
        _reverseRowOrderCheck = null;

        var localTop = 0;

        void AddLocalLabel(string text)
        {
            _formatOptionsPanel.Controls.Add(new Label { Text = text, Left = 0, Top = localTop, Width = 300, Height = 18 });
            localTop += 20;
        }

        // Every control below seeds its initial value from _lastSettings instead of a hardcoded
        // literal, so reopening this dialog resumes with whatever was last actually used to print.
        // Label count lives solely in _quantityUpDown (see class doc) — no per-format copies field here.
        switch (SelectedFormat())
        {
            case PrintFormat.Pdf:
                break;

            case PrintFormat.PplaNative:
            case PrintFormat.PplaRaster:
                AddLocalLabel("Escurecimento (2-20)");
                _darknessUpDown = new NumericUpDown { Minimum = 2, Maximum = 20, Value = Math.Clamp(_lastSettings.Darkness, 2, 20), Width = 80, Top = localTop };
                _formatOptionsPanel.Controls.Add(_darknessUpDown);
                localTop += 30;

                AddLocalLabel("Tipo de impressão");
                _transferTypeCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300, Top = localTop };
                _transferTypeCombo.Items.AddRange(["Térmica direta", "Transferência térmica (ribbon)"]);
                _transferTypeCombo.SelectedIndex = _lastSettings.TransferType == ArgoxTransferTypeSetting.ThermalTransfer ? 1 : 0;
                _formatOptionsPanel.Controls.Add(_transferTypeCombo);
                localTop += 30;

                // Manual PPLA calibration — PPLA bypasses the Windows driver entirely (raw byte
                // stream), so there's no way to read a printer's own top-of-form/gap-sensor
                // calibration automatically; these are found by trial print, same as BarTender's own
                // "print offset" setting. FeedOffsetMm in particular is what fixes "o papel avança
                // muito" (the printer over/under-feeding relative to the label's own defined height) —
                // it was already wired into PplaCommandBuilder's label-length command, just never
                // exposed here, so it silently stayed at 0 (no correction) for every print.
                AddLocalLabel("Deslocamento de impressão (mm)");
                _formatOptionsPanel.Controls.Add(new Label { Text = "X", Left = 0, Top = localTop + 3, Width = 16, Height = 16 });
                _offsetXUpDown = new NumericUpDown { Left = 18, Top = localTop, Width = 110, Minimum = -100, Maximum = 100, DecimalPlaces = 1, Increment = 0.5m, Value = Math.Clamp((decimal)_lastSettings.OffsetXMm, -100, 100) };
                _formatOptionsPanel.Controls.Add(_offsetXUpDown);
                _formatOptionsPanel.Controls.Add(new Label { Text = "Y", Left = 150, Top = localTop + 3, Width = 16, Height = 16 });
                _offsetYUpDown = new NumericUpDown { Left = 168, Top = localTop, Width = 110, Minimum = -100, Maximum = 100, DecimalPlaces = 1, Increment = 0.5m, Value = Math.Clamp((decimal)_lastSettings.OffsetYMm, -100, 100) };
                _formatOptionsPanel.Controls.Add(_offsetYUpDown);
                localTop += 30;

                AddLocalLabel("Avanço de papel (mm)");
                _feedOffsetUpDown = new NumericUpDown { Left = 0, Top = localTop, Width = 110, Minimum = -200, Maximum = 200, DecimalPlaces = 1, Increment = 0.5m, Value = Math.Clamp((decimal)_lastSettings.FeedOffsetMm, -200, 200) };
                _formatOptionsPanel.Controls.Add(_feedOffsetUpDown);
                localTop += 30;
                _formatOptionsPanel.Controls.Add(new Label
                {
                    Text = "Quanto a impressora avança por etiqueta, além do tamanho desenhado. Negativo reduz o avanço.",
                    Left = 0,
                    Top = localTop,
                    Width = 300,
                    Height = 30,
                    ForeColor = SystemColors.GrayText,
                });
                localTop += 30;

                if (SelectedFormat() == PrintFormat.PplaRaster)
                {
                    _fullResolutionCheck = new CheckBox { Text = "Resolução total do cabeçote (D11)", Checked = _lastSettings.FullResolution, Left = 0, Top = localTop, Width = 300 };
                    _formatOptionsPanel.Controls.Add(_fullResolutionCheck);
                    localTop += 24;

                    _mirrorHorizontalCheck = new CheckBox { Text = "Espelhar horizontalmente", Checked = _lastSettings.MirrorHorizontal, Left = 0, Top = localTop, Width = 300 };
                    _formatOptionsPanel.Controls.Add(_mirrorHorizontalCheck);
                    localTop += 24;

                    _reverseRowOrderCheck = new CheckBox { Text = "Inverter ordem das linhas", Checked = _lastSettings.ReverseRowOrder, Left = 0, Top = localTop, Width = 300 };
                    _formatOptionsPanel.Controls.Add(_reverseRowOrderCheck);
                }

                break;
        }

        UpdatePreview();
    }

    private PrintFormat SelectedFormat() => (PrintFormat)_formatCombo.SelectedIndex;

    private string? SelectedPrinterName() => _printerCombo.SelectedIndex <= 0 ? null : (string)_printerCombo.SelectedItem!;

    private void UpdatePreview()
    {
        try
        {
            if (_isBatchMode)
            {
                // Only the first physical row needs rendering here — it's a preview, and every row
                // after the first is laid out identically (just with different records in each slot).
                var rows = ResolveBatchRows();
                _preview.Document = rows.Count > 0 ? rows[0] : null;
            }
            else
            {
                _preview.Document = Resolve();
            }
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

    /// <summary>Every physical row this print job will produce — each one <see cref="PageConfig.Columns"/>
    /// labels wide, tiled from <see cref="BuildRecords"/> by <c>LayoutEngine.ResolveBatch</c>.</summary>
    private IReadOnlyList<ResolvedDocument> ResolveBatchRows() => new LayoutEngine().ResolveBatch(_document, BuildRecords());

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

    private ArgoxRendererOptions BuildArgoxRendererOptions(int copies) => new()
    {
        Darkness = (int)(_darknessUpDown?.Value ?? 10),
        Copies = copies,
        TransferType = _transferTypeCombo?.SelectedIndex == 1 ? ArgoxTransferType.ThermalTransfer : ArgoxTransferType.DirectThermal,
        OffsetXMm = (double)(_offsetXUpDown?.Value ?? 0),
        OffsetYMm = (double)(_offsetYUpDown?.Value ?? 0),
        FeedOffsetMm = (double)(_feedOffsetUpDown?.Value ?? 0),
    };

    private void PrintNow()
    {
        try
        {
            var format = SelectedFormat();
            var target = SelectedPrinterName();

            IReadOnlyList<ResolvedDocument> rows;
            int perRowCopies;
            if (_isBatchMode)
            {
                // The row count from ResolveBatch already *is* the requested quantity (see class doc)
                // — the printer/driver must not additionally repeat each row on top of that.
                rows = ResolveBatchRows();
                perRowCopies = 1;
                if (rows.Count == 0)
                {
                    MessageBox.Show(this, "Adicione ao menos um registro antes de imprimir.", "Imprimir etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            else
            {
                rows = [Resolve()];
                perRowCopies = (int)_quantityUpDown.Value;
            }

            var totalBytes = PrintRows(format, target, rows, perRowCopies);
            PrintSettingsStore.Save(CaptureCurrentSettings(format, target));

            var message = rows.Count > 1
                ? $"Enviado para {target ?? "a impressora padrão"}: {rows.Count} fileira(s) impressa(s) ({totalBytes} bytes)."
                : $"Enviado para {target ?? "a impressora padrão"} ({totalBytes} bytes).";
            MessageBox.Show(this, message, "Imprimir etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Falha ao imprimir: {ex.Message}", "Imprimir etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>Sends every row to the printer and returns the total byte count sent (just for the
    /// success message). PDF batches naturally into one multi-page file/one transport send — PdfSharp
    /// has no equivalent of a printer's raw byte stream needing per-row framing. PPLA has no
    /// multi-label-job concept in this codebase's transport layer, so each row goes as its own
    /// separate raw send instead — functionally one physical print per row, same as printing each
    /// row's label individually back to back.</summary>
    private int PrintRows(PrintFormat format, string? target, IReadOnlyList<ResolvedDocument> rows, int perRowCopies)
    {
        switch (format)
        {
            case PrintFormat.Pdf:
            {
                var bytes = PdfExporter.ExportBatch(rows);
                var transport = new WindowsPdfPrintTransport { Copies = perRowCopies };
                transport.Send(bytes, target);
                return bytes.Length;
            }

            case PrintFormat.PplaNative:
            {
                var totalBytes = 0;
                var transport = new WindowsRawPrintTransport();
                foreach (var row in rows)
                {
                    var bytes = PplaCommandBuilder.Build(row, BuildArgoxRendererOptions(perRowCopies));
                    transport.Send(bytes, target);
                    totalBytes += bytes.Length;
                }

                return totalBytes;
            }

            case PrintFormat.PplaRaster:
            {
                var totalBytes = 0;
                var transport = new WindowsRawPrintTransport();
                foreach (var row in rows)
                {
                    var bytes = PplaRasterBuilder.Build(row, new ArgoxRasterOptions
                    {
                        Base = BuildArgoxRendererOptions(perRowCopies),
                        FullResolution = _fullResolutionCheck?.Checked ?? true,
                        MirrorHorizontal = _mirrorHorizontalCheck?.Checked ?? true,
                        ReverseRowOrder = _reverseRowOrderCheck?.Checked ?? false,
                    });
                    transport.Send(bytes, target);
                    totalBytes += bytes.Length;
                }

                return totalBytes;
            }

            default:
                throw new InvalidOperationException($"Unsupported print format '{format}'.");
        }
    }

    /// <summary>Snapshots the settings that were actually used for the print job that just succeeded
    /// — saved by <see cref="PrintNow"/> so the next "Imprimir..." reopens with these, not defaults.</summary>
    private PrintSettings CaptureCurrentSettings(PrintFormat format, string? printerName) => new()
    {
        PrinterName = printerName,
        Format = format,
        Quantity = (int)_quantityUpDown.Value,
        Darkness = (int)(_darknessUpDown?.Value ?? _lastSettings.Darkness),
        TransferType = _transferTypeCombo is null
            ? _lastSettings.TransferType
            : _transferTypeCombo.SelectedIndex == 1 ? ArgoxTransferTypeSetting.ThermalTransfer : ArgoxTransferTypeSetting.DirectThermal,
        OffsetXMm = (double)(_offsetXUpDown?.Value ?? (decimal)_lastSettings.OffsetXMm),
        OffsetYMm = (double)(_offsetYUpDown?.Value ?? (decimal)_lastSettings.OffsetYMm),
        FeedOffsetMm = (double)(_feedOffsetUpDown?.Value ?? (decimal)_lastSettings.FeedOffsetMm),
        FullResolution = _fullResolutionCheck?.Checked ?? _lastSettings.FullResolution,
        MirrorHorizontal = _mirrorHorizontalCheck?.Checked ?? _lastSettings.MirrorHorizontal,
        ReverseRowOrder = _reverseRowOrderCheck?.Checked ?? _lastSettings.ReverseRowOrder,
    };
}
