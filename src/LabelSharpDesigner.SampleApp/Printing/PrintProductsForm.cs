using System.ComponentModel;
using System.Globalization;
using LabelSharpDesigner.App;
using LabelSharpDesigner.App.Library;
using LabelSharpDesigner.Core.Document;
using LabelSharpDesigner.Core.Layout;
using LabelSharpDesigner.Layout;
using LabelSharpDesigner.PrintTransport.Windows;
using LabelSharpDesigner.Rendering.ArgoxPpla;
using LabelSharpDesigner.Rendering.Pdf;
using LabelSharpDesigner.SampleApp.Products;

namespace LabelSharpDesigner.SampleApp.Printing;

/// <summary>
/// Batch-print screen: pick products and how many labels each needs, pick which saved label
/// template to print them on, and send the result to a printer. Always mail-merge/batch mode
/// (<see cref="LayoutEngine.ResolveBatch"/>) — unlike the main App's <c>PrintDialogForm</c>, there is
/// no single-document "cópias" mode here, since printing several different products is inherently a
/// multi-record job regardless of the label's own column count.
///
/// <para>"Colunas" is deliberately independent of the selected label's own <see cref="PageConfig.Columns"/>:
/// it describes the physical roll/printer setup currently in front of the user, so it persists
/// (<see cref="PrintColumnsSettingsStore"/>) across label choices and app runs once first used —
/// the label's own column count is only ever a one-time suggestion for it (see
/// <see cref="OnLabelSelectionChanged"/>).</para>
/// </summary>
public sealed class PrintProductsForm : Form
{
    private readonly ProductRepository _productRepository;
    private readonly LibraryRepository _labelRepository;
    private readonly LayoutEngine _layoutEngine = new();
    private readonly BindingList<ProductPrintRow> _rows;

    private readonly ComboBox _labelCombo;
    private readonly NumericUpDown _columnsUpDown;
    private readonly ComboBox _printerCombo;
    private readonly ComboBox _formatCombo;
    private readonly Panel _formatOptionsPanel;
    private readonly DataGridView _grid;
    private readonly LabelPreviewControl _preview;
    private readonly Label _previewErrorLabel;

    private NumericUpDown? _darknessUpDown;
    private ComboBox? _transferTypeCombo;
    private NumericUpDown? _feedOffsetUpDown;
    private CheckBox? _fullResolutionCheck;

    /// <summary>Null until the user successfully prints at least once (this run or a previous one) —
    /// see class doc for what that changes about "Colunas".</summary>
    private int? _persistedColumns = PrintColumnsSettingsStore.Load().Columns;

    /// <summary>User-configured product-field ↔ label-variable bindings, keyed by label id then by
    /// variable name — see <see cref="VariableMappingForm"/>. Loaded once and mutated in place as the
    /// user (re)configures a label's bindings, saved back to disk on every change.</summary>
    private readonly Dictionary<string, Dictionary<string, ProductFieldSource>> _fieldMappings = VariableFieldMappingStore.Load();

    public PrintProductsForm(ProductRepository productRepository, LibraryRepository labelRepository)
    {
        _productRepository = productRepository;
        _labelRepository = labelRepository;
        _rows = new BindingList<ProductPrintRow>(
            _productRepository.List().Select(product => new ProductPrintRow { Product = product }).ToList());

        Text = "Imprimir etiquetas";
        Width = 1180;
        Height = 720;
        StartPosition = FormStartPosition.CenterScreen;

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };
        var cancelButton = new Button { Text = "Fechar", DialogResult = DialogResult.Cancel, Width = 90, FlatStyle = FlatStyle.System };
        var printButton = new Button { Text = "Imprimir", Width = 110, FlatStyle = FlatStyle.System };
        printButton.Click += (_, _) => PrintNow();
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(printButton);

        var gridPanel = new Panel { Dock = DockStyle.Fill };
        _grid = BuildProductsGrid();
        gridPanel.Controls.Add(_grid);

        var configPanel = new Panel { Dock = DockStyle.Left, Width = 320, Padding = new Padding(12) };
        var configContent = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        configPanel.Controls.Add(configContent);

        var previewPanel = new Panel { Dock = DockStyle.Right, Width = 320 };
        _preview = new LabelPreviewControl { Dock = DockStyle.Fill };
        // Shown instead of _preview (explicit Visible toggling in UpdatePreview, never relying on
        // z-order) whenever resolving the batch throws — e.g. an unmapped Number variable whose label
        // default doesn't parse. Blanking the preview with no explanation was the exact bug fixed in
        // the main editor's own "Pré-visualizar" tab; this mirrors that fix here.
        _previewErrorLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(16),
            ForeColor = Color.Firebrick,
            Visible = false,
        };
        previewPanel.Controls.Add(_preview);
        previewPanel.Controls.Add(_previewErrorLabel);

        Controls.Add(gridPanel);
        Controls.Add(configPanel);
        Controls.Add(previewPanel);
        Controls.Add(buttonPanel);

        var top = 0;

        Label AddLabel(string text)
        {
            var label = new Label { Text = text, Left = 0, Top = top, Width = 296, Height = 18 };
            configContent.Controls.Add(label);
            top += 20;
            return label;
        }

        AddLabel("Etiqueta");
        _labelCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 296, Top = top };
        _labelCombo.SelectedIndexChanged += (_, _) => OnLabelSelectionChanged();
        configContent.Controls.Add(_labelCombo);
        top += 30;

        var mappingButton = new Button { Text = "Vincular campos...", Left = 0, Top = top, Width = 296, FlatStyle = FlatStyle.System };
        mappingButton.Click += (_, _) => OpenFieldMappingDialog();
        configContent.Controls.Add(mappingButton);
        top += 34;

        AddLabel("Colunas (etiquetas por fileira)");
        _columnsUpDown = new NumericUpDown { Left = 0, Top = top, Width = 100, Minimum = 1, Maximum = 99, Value = 1 };
        _columnsUpDown.ValueChanged += (_, _) => UpdatePreview();
        configContent.Controls.Add(_columnsUpDown);
        top += 34;

        AddLabel("Impressora");
        _printerCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 296, Top = top };
        _printerCombo.Items.Add("(Impressora padrão do sistema)");
        foreach (var printer in new WindowsPrinterDiscovery().ListAvailable())
        {
            _printerCombo.Items.Add(printer);
        }

        _printerCombo.SelectedIndex = 0;
        configContent.Controls.Add(_printerCombo);
        top += 34;

        AddLabel("Formato");
        _formatCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 296, Top = top };
        _formatCombo.Items.AddRange(["PDF (via driver, qualquer impressora)", "PPLA nativo (Argox)", "PPLA raster (Argox — texto/imagem/QR)"]);
        _formatCombo.SelectedIndexChanged += (_, _) => RebuildForFormat();
        configContent.Controls.Add(_formatCombo);
        top += 34;

        _formatOptionsPanel = new Panel { Left = 0, Top = top, Width = 296, Height = 200 };
        configContent.Controls.Add(_formatOptionsPanel);

        LoadLabelEntries();
        // Always defaults to PPLA raster — this app's print flow is fixed on raster/thermal-transfer
        // output, never a hardcoded PDF default.
        _formatCombo.SelectedIndex = (int)PrintDialogForm.PrintFormat.PplaRaster;
    }

    private DataGridView BuildProductsGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoGenerateColumns = false,
            DataSource = _rows,
        };

        grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", HeaderText = "Imprimir", DataPropertyName = "Selected", FillWeight = 60 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "Descrição", DataPropertyName = "Description", ReadOnly = true, FillWeight = 220 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Price", HeaderText = "Preço", DataPropertyName = "Price", ReadOnly = true, FillWeight = 80, DefaultCellStyle = { Format = "C2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Barcode", HeaderText = "Código de barras", DataPropertyName = "Barcode", ReadOnly = true, FillWeight = 140 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Quantity", HeaderText = "Quantidade", DataPropertyName = "Quantity", FillWeight = 90 });

        // Checkbox columns don't commit their new value to the bound object until the cell loses
        // focus unless forced — without this, toggling a row and immediately hitting "Imprimir"
        // would silently print against the previous, stale Selected value.
        grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (grid.IsCurrentCellDirty)
            {
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        grid.CellValueChanged += (_, _) => UpdatePreview();

        return grid;
    }

    private void LoadLabelEntries()
    {
        _labelCombo.Items.Clear();
        foreach (var entry in _labelRepository.List())
        {
            _labelCombo.Items.Add(new LabelComboItem(entry));
        }

        if (_labelCombo.Items.Count > 0)
        {
            _labelCombo.SelectedIndex = 0;
        }
    }

    /// <summary>Only overwrites "Colunas" with the newly selected label's own <see cref="PageConfig.Columns"/>
    /// while no persisted value exists yet (first run) — see class doc.</summary>
    private void OnLabelSelectionChanged()
    {
        if (SelectedEntry() is { } entry)
        {
            var suggested = _persistedColumns ?? entry.Document.Page.Columns;
            _columnsUpDown.Value = Math.Clamp(suggested, (int)_columnsUpDown.Minimum, (int)_columnsUpDown.Maximum);
        }

        UpdatePreview();
    }

    private LibraryEntry? SelectedEntry() => (_labelCombo.SelectedItem as LabelComboItem)?.Entry;

    private void RebuildForFormat()
    {
        foreach (Control control in _formatOptionsPanel.Controls.Cast<Control>().ToList())
        {
            control.Dispose();
        }

        _formatOptionsPanel.Controls.Clear();
        _darknessUpDown = null;
        _transferTypeCombo = null;
        _feedOffsetUpDown = null;
        _fullResolutionCheck = null;

        if (SelectedFormat() != PrintDialogForm.PrintFormat.Pdf)
        {
            var localTop = 0;

            _formatOptionsPanel.Controls.Add(new Label { Text = "Escurecimento (2-20)", Left = 0, Top = localTop, Width = 296, Height = 18 });
            localTop += 20;
            _darknessUpDown = new NumericUpDown { Minimum = 2, Maximum = 20, Value = 10, Width = 80, Top = localTop };
            _formatOptionsPanel.Controls.Add(_darknessUpDown);
            localTop += 34;

            _formatOptionsPanel.Controls.Add(new Label { Text = "Tipo de impressão", Left = 0, Top = localTop, Width = 296, Height = 18 });
            localTop += 20;
            _transferTypeCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 296, Top = localTop };
            _transferTypeCombo.Items.AddRange(["Térmica direta", "Transferência térmica (ribbon)"]);
            // Always defaults to "ribbon", same fixed-default reasoning as the format combo above.
            _transferTypeCombo.SelectedIndex = 1;
            _formatOptionsPanel.Controls.Add(_transferTypeCombo);
            localTop += 34;

            // Corrects how far the printer physically feeds per label — the same "o papel avança
            // muito/pouco" calibration PrintDialogForm exposes, found by trial print (PPLA bypasses
            // the Windows driver entirely, so there's no way to read a printer's own calibration).
            _formatOptionsPanel.Controls.Add(new Label { Text = "Avanço de etiqueta (mm)", Left = 0, Top = localTop, Width = 296, Height = 18 });
            localTop += 20;
            _feedOffsetUpDown = new NumericUpDown { Left = 0, Top = localTop, Width = 110, Minimum = -200, Maximum = 200, DecimalPlaces = 1, Increment = 0.5m, Value = 0 };
            _formatOptionsPanel.Controls.Add(_feedOffsetUpDown);
            localTop += 34;

            // Only meaningful for the raster path — native PPLA sends drawing commands straight to
            // the firmware, so there's no "printhead resolution" to trade off there.
            if (SelectedFormat() == PrintDialogForm.PrintFormat.PplaRaster)
            {
                _fullResolutionCheck = new CheckBox { Text = "Qualidade — resolução total do cabeçote (D11)", Checked = true, Left = 0, Top = localTop, Width = 296 };
                _formatOptionsPanel.Controls.Add(_fullResolutionCheck);
            }
        }

        UpdatePreview();
    }

    private PrintDialogForm.PrintFormat SelectedFormat() => (PrintDialogForm.PrintFormat)_formatCombo.SelectedIndex;

    private string? SelectedPrinterName() => _printerCombo.SelectedIndex <= 0 ? null : (string)_printerCombo.SelectedItem!;

    /// <summary>The selected label's document with "Colunas" swapped for whatever the user set here —
    /// the label template's own authored column count is only ever the initial suggestion for that
    /// field (see class doc), never a silent override of it at print time.</summary>
    private LabelDocument? DocumentForPrinting()
    {
        var entry = SelectedEntry();
        if (entry is null)
        {
            return null;
        }

        return entry.Document with { Page = entry.Document.Page with { Columns = (int)_columnsUpDown.Value } };
    }

    /// <summary>Opens the field-binding dialog for the currently selected label, seeded from whatever
    /// was saved for it before (or a one-time alias-based guess for a label never configured yet — see
    /// <see cref="GuessDefaultSource"/>), and persists the result for next time.</summary>
    private void OpenFieldMappingDialog()
    {
        if (SelectedEntry() is not { } entry)
        {
            MessageBox.Show(this, "Selecione uma etiqueta antes de vincular os campos.", "Vincular campos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dialog = new VariableMappingForm(entry.Document, ResolveMappingFor(entry));
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result is not { } mapping)
        {
            return;
        }

        _fieldMappings[entry.Id] = mapping;
        VariableFieldMappingStore.Save(_fieldMappings);
        UpdatePreview();
    }

    /// <summary>The binding to use for <paramref name="entry"/>'s variables: whatever was last saved
    /// for it, topped up with an alias-based guess (see <see cref="GuessDefaultSource"/>) for any
    /// variable the label declares that isn't in that saved set yet — e.g. a variable added to the
    /// label after the last time its bindings were configured.</summary>
    private Dictionary<string, ProductFieldSource> ResolveMappingFor(LibraryEntry entry)
    {
        if (!_fieldMappings.TryGetValue(entry.Id, out var saved))
        {
            saved = new Dictionary<string, ProductFieldSource>(StringComparer.Ordinal);
        }

        foreach (var variable in entry.Document.Variables)
        {
            if (!saved.ContainsKey(variable.Name))
            {
                saved[variable.Name] = GuessDefaultSource(variable);
            }
        }

        return saved;
    }

    /// <summary>A one-time starting suggestion for a variable nobody has explicitly bound yet, by
    /// matching common Portuguese/English aliases against its name — the user is always free to
    /// override this in <see cref="VariableMappingForm"/>; it is never applied again once a binding
    /// for that variable has been saved.</summary>
    private static ProductFieldSource GuessDefaultSource(LabelVariable variable)
    {
        var key = variable.Name.Trim().ToLowerInvariant().Replace("_", "").Replace(" ", "");
        return key switch
        {
            "descricao" or "description" or "produto" or "nome" => ProductFieldSource.Description,
            "preco" or "price" or "valor" => ProductFieldSource.Price,
            "codigobarras" or "barcode" or "ean" or "codigo" => ProductFieldSource.Barcode,
            _ => ProductFieldSource.LabelDefault,
        };
    }

    /// <summary>One record per physical label — a selected row with quantity N contributes N
    /// identical records, one per label that product needs.</summary>
    private IReadOnlyList<IReadOnlyDictionary<string, object?>> BuildRecords(LibraryEntry entry)
    {
        var mapping = ResolveMappingFor(entry);
        var records = new List<IReadOnlyDictionary<string, object?>>();
        foreach (var row in _rows)
        {
            if (!row.Selected || row.Quantity <= 0)
            {
                continue;
            }

            var record = BuildRecord(row.Product, entry.Document.Variables, mapping);
            for (var i = 0; i < row.Quantity; i++)
            {
                records.Add(record);
            }
        }

        return records;
    }

    /// <summary>Fills a record for <paramref name="product"/> using the user-configured
    /// <paramref name="mapping"/> — any variable with no binding (or bound to
    /// <see cref="ProductFieldSource.LabelDefault"/>) falls back to its own authored default value,
    /// parsed per its declared <see cref="LabelVariable.Type"/> (mirrors <c>EditorForm.ParseSampleValue</c>)
    /// rather than passed through as a raw string — a <c>Number</c> variable used unmapped in an
    /// expression like <c>{{ variavel + 1 }}</c> must add, not concatenate.</summary>
    private static Dictionary<string, object?> BuildRecord(Product product, IReadOnlyList<LabelVariable> variables, IReadOnlyDictionary<string, ProductFieldSource> mapping)
    {
        var record = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var variable in variables)
        {
            var source = mapping.TryGetValue(variable.Name, out var value) ? value : ProductFieldSource.LabelDefault;
            record[variable.Name] = source switch
            {
                ProductFieldSource.Description => product.Description,
                ProductFieldSource.Price => (double)product.Price,
                ProductFieldSource.Barcode => product.Barcode,
                _ => ParseDefaultValue(variable),
            };
        }

        return record;
    }

    private static object? ParseDefaultValue(LabelVariable variable) => variable.Type switch
    {
        VariableValueType.Number => double.TryParse(variable.DefaultValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : variable.DefaultValue,
        VariableValueType.Date => DateTime.TryParse(variable.DefaultValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : variable.DefaultValue,
        VariableValueType.Boolean => bool.TryParse(variable.DefaultValue, out var boolean) ? boolean : variable.DefaultValue,
        _ => variable.DefaultValue,
    };

    private IReadOnlyList<ResolvedDocument> ResolveBatchRows()
    {
        var entry = SelectedEntry();
        var document = DocumentForPrinting();
        if (entry is null || document is null)
        {
            return [];
        }

        var records = BuildRecords(entry);
        return records.Count == 0 ? [] : _layoutEngine.ResolveBatch(document, records);
    }

    private void UpdatePreview()
    {
        try
        {
            var rows = ResolveBatchRows();
            _preview.Document = rows.Count > 0 ? rows[0] : null;
            _previewErrorLabel.Visible = false;
            _preview.Visible = true;
        }
        catch (Exception ex)
        {
            _preview.Document = null;
            _previewErrorLabel.Text = $"Não foi possível gerar a pré-visualização:\n\n{ex.Message}";
            _preview.Visible = false;
            _previewErrorLabel.Visible = true;
        }

        _preview.Invalidate();
    }

    private void PrintNow()
    {
        try
        {
            var entry = SelectedEntry();
            var document = DocumentForPrinting();
            if (entry is null || document is null)
            {
                MessageBox.Show(this, "Cadastre e selecione uma etiqueta antes de imprimir.", "Imprimir etiquetas", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var records = BuildRecords(entry);
            if (records.Count == 0)
            {
                MessageBox.Show(this, "Selecione ao menos um produto com quantidade maior que zero.", "Imprimir etiquetas", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var rows = _layoutEngine.ResolveBatch(document, records);
            var format = SelectedFormat();
            var target = SelectedPrinterName();
            var totalBytes = PrintRows(format, target, rows);

            PrintColumnsSettingsStore.Save(new PrintColumnsSettings { Columns = (int)_columnsUpDown.Value });
            _persistedColumns = (int)_columnsUpDown.Value;

            MessageBox.Show(
                this,
                $"Enviado para {target ?? "a impressora padrão"}: {records.Count} etiqueta(s) em {rows.Count} fileira(s) ({totalBytes} bytes).",
                "Imprimir etiquetas",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Falha ao imprimir: {ex.Message}", "Imprimir etiquetas", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>Sends every physical row to the printer — mirrors <c>PrintDialogForm.PrintRows</c>,
    /// always with a single copy per row since the requested quantity is already baked into how many
    /// records were tiled into these rows.</summary>
    private int PrintRows(PrintDialogForm.PrintFormat format, string? target, IReadOnlyList<ResolvedDocument> rows)
    {
        switch (format)
        {
            case PrintDialogForm.PrintFormat.Pdf:
            {
                var bytes = PdfExporter.ExportBatch(rows);
                new WindowsPdfPrintTransport { Copies = 1 }.Send(bytes, target);
                return bytes.Length;
            }

            case PrintDialogForm.PrintFormat.PplaNative:
            {
                var totalBytes = 0;
                var transport = new WindowsRawPrintTransport();
                foreach (var row in rows)
                {
                    var bytes = PplaCommandBuilder.Build(row, BuildArgoxRendererOptions());
                    transport.Send(bytes, target);
                    totalBytes += bytes.Length;
                }

                return totalBytes;
            }

            case PrintDialogForm.PrintFormat.PplaRaster:
            {
                var totalBytes = 0;
                var transport = new WindowsRawPrintTransport();
                foreach (var row in rows)
                {
                    var bytes = PplaRasterBuilder.Build(row, new ArgoxRasterOptions
                    {
                        Base = BuildArgoxRendererOptions(),
                        FullResolution = _fullResolutionCheck?.Checked ?? true,
                        MirrorHorizontal = false,
                        ReverseRowOrder = false,
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

    private ArgoxRendererOptions BuildArgoxRendererOptions() => new()
    {
        Darkness = (int)(_darknessUpDown?.Value ?? 10),
        Copies = 1,
        TransferType = _transferTypeCombo?.SelectedIndex == 1 ? ArgoxTransferType.ThermalTransfer : ArgoxTransferType.DirectThermal,
        FeedOffsetMm = (double)(_feedOffsetUpDown?.Value ?? 0),
    };

    private sealed record LabelComboItem(LibraryEntry Entry)
    {
        public override string ToString() => Entry.Document.Name;
    }
}
