using LabelSharpDesigner.Core.Document;
using LabelSharpDesigner.Core.Elements;
using LabelSharpDesigner.Core.Geometry;
using LabelSharpDesigner.Core.Layout;
using LabelSharpDesigner.Layout;
using LabelSharpDesigner.UI.WinForms.Canvas;
using LabelSharpDesigner.UI.WinForms.Panels;

namespace LabelSharpDesigner.App;

public sealed class EditorForm : Form
{
    private readonly LabelCanvasControl _canvas;
    private readonly Label _statusLabel;
    private readonly NumericUpDown _positionXField;
    private readonly NumericUpDown _positionYField;
    private readonly ToolStripButton _undoButton;
    private readonly ToolStripButton _redoButton;
    private readonly ToolStripLabel _zoomLabel;
    private readonly Action<LabelDocument>? _onSave;
    private bool _suppressGeometryFieldEvents;

    /// <summary>
    /// Opens an editor over <paramref name="document"/>. Persistence is entirely the caller's
    /// responsibility: nothing here writes to disk on its own — the "Salvar" toolbar button (shown only
    /// when <paramref name="onSave"/> is provided) hands the current in-memory document back through
    /// the callback, mirroring the original Flutter <c>LabelDesigner.onSave</c> hand-off to
    /// <c>EditorScreen</c>/<c>LibraryRepository</c>.
    /// </summary>
    public EditorForm(LabelDocument document, Action<LabelDocument>? onSave = null)
    {
        _onSave = onSave;
        var layout = EditorLayoutSettingsStore.Load();

        Text = "LabelSharpDesigner — Editor";
        Width = 1200;
        Height = 800;
        StartPosition = FormStartPosition.CenterScreen;

        var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 26 };

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            Text = "Nenhuma seleção",
        };

        // X/Y live here instead of in the property panel: this bar updates on every mouse-move of a
        // drag (LabelCanvasControl.LiveChanged), which is cheap (just NumericUpDown.Value), whereas
        // the property panel only rebuilds on a committed change to keep dragging fluid.
        var geometryPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
            Padding = new Padding(0, 4, 8, 0),
        };
        geometryPanel.Controls.Add(new Label { Text = "X (mm):", AutoSize = true, Margin = new Padding(0, 4, 2, 0) });
        _positionXField = new NumericUpDown { Minimum = -2000, Maximum = 2000, DecimalPlaces = 2, Width = 70, Margin = new Padding(0, 1, 12, 0), Enabled = false };
        geometryPanel.Controls.Add(_positionXField);
        geometryPanel.Controls.Add(new Label { Text = "Y (mm):", AutoSize = true, Margin = new Padding(0, 4, 2, 0) });
        _positionYField = new NumericUpDown { Minimum = -2000, Maximum = 2000, DecimalPlaces = 2, Width = 70, Margin = new Padding(0, 1, 0, 0), Enabled = false };
        geometryPanel.Controls.Add(_positionYField);

        statusBar.Controls.Add(_statusLabel);
        statusBar.Controls.Add(geometryPanel);

        _canvas = new LabelCanvasControl { Dock = DockStyle.Fill };
        _canvas.SelectionChanged += (_, _) => { UpdateStatus(); UpdateGeometryFields(); };
        _canvas.DocumentChanged += (_, _) => { UpdateStatus(); UpdateGeometryFields(); };
        _canvas.LiveChanged += (_, _) => UpdateGeometryFields();

        _positionXField.ValueChanged += (_, _) =>
        {
            if (_suppressGeometryFieldEvents)
            {
                return;
            }

            _canvas.ApplyPropertyChange(e => e with { Position = e.Position with { X = (double)_positionXField.Value } }, "Posição X");
        };
        _positionYField.ValueChanged += (_, _) =>
        {
            if (_suppressGeometryFieldEvents)
            {
                return;
            }

            _canvas.ApplyPropertyChange(e => e with { Position = e.Position with { Y = (double)_positionYField.Value } }, "Posição Y");
        };

        var layersPanel = new LayersPanel { Dock = DockStyle.Left, Width = Math.Max(layout.LayersPanelWidth, 120), MinimumSize = new Size(120, 0) };
        // A Splitter docked on the same edge, added right after its panel, gives that panel a
        // drag-to-resize handle (see the Controls.Add order below for why add-order matters here).
        var layersSplitter = new Splitter { Dock = DockStyle.Left, Width = 4 };
        // Shown instead of layersPanel/layersSplitter while collapsed — a thin strip with just a
        // reopen button, the same "auto-hide" shape Visual Studio leaves behind for a closed tool
        // window, so the panel is never more than one click away even when hidden.
        var layersCollapsedStrip = new Panel { Dock = DockStyle.Left, Width = 24, Visible = false };
        var layersReopenButton = new Button { Text = "▶", Dock = DockStyle.Top, Height = 24, FlatStyle = FlatStyle.System };
        layersCollapsedStrip.Controls.Add(layersReopenButton);

        var propertyPanel = new PropertyPanel { Dock = DockStyle.Fill };
        var previewControl = new RenderPreviewControl { Dock = DockStyle.Fill };

        // The preview lives in its own tab right next to Propriedades, always one click away,
        // instead of a togglable panel that ate canvas space — it only actually re-renders while its
        // tab is the one showing, so switching to Propriedades doesn't pay the layout-engine cost.
        var propertiesTab = new TabPage("Propriedades");
        propertiesTab.Controls.Add(propertyPanel);
        var previewTab = new TabPage("Pré-visualizar");
        previewTab.Controls.Add(previewControl);
        var sidePanelTabs = new TabControl { Dock = DockStyle.Fill };
        sidePanelTabs.TabPages.Add(propertiesTab);
        sidePanelTabs.TabPages.Add(previewTab);

        // A thin header above the tab strip, just for a close button — TabControl has no built-in
        // header of its own to put one on, unlike LayersPanel.
        var sidePanelHeader = new Panel { Dock = DockStyle.Top, Height = 22 };
        var sidePanelCloseButton = new Button { Text = "✕", Width = 24, Height = 20, Dock = DockStyle.Right, FlatStyle = FlatStyle.System };
        sidePanelHeader.Controls.Add(sidePanelCloseButton);

        var sidePanelContainer = new Panel { Dock = DockStyle.Right, Width = Math.Max(layout.SidePanelWidth, 160), MinimumSize = new Size(160, 0) };
        sidePanelContainer.Controls.Add(sidePanelTabs);
        sidePanelContainer.Controls.Add(sidePanelHeader);
        var sidePanelSplitter = new Splitter { Dock = DockStyle.Right, Width = 4 };
        var sidePanelCollapsedStrip = new Panel { Dock = DockStyle.Right, Width = 24, Visible = false };
        var sidePanelReopenButton = new Button { Text = "◀", Dock = DockStyle.Top, Height = 24, FlatStyle = FlatStyle.System };
        sidePanelCollapsedStrip.Controls.Add(sidePanelReopenButton);

        void SetLayersVisible(bool visible)
        {
            layersPanel.Visible = visible;
            layersSplitter.Visible = visible;
            layersCollapsedStrip.Visible = !visible;
            layout.LayersPanelVisible = visible;
            EditorLayoutSettingsStore.Save(layout);
        }

        void SetSidePanelVisible(bool visible)
        {
            sidePanelContainer.Visible = visible;
            sidePanelSplitter.Visible = visible;
            sidePanelCollapsedStrip.Visible = !visible;
            layout.SidePanelVisible = visible;
            EditorLayoutSettingsStore.Save(layout);
        }

        layersPanel.CloseRequested += (_, _) => SetLayersVisible(false);
        layersReopenButton.Click += (_, _) => SetLayersVisible(true);
        sidePanelCloseButton.Click += (_, _) => SetSidePanelVisible(false);
        sidePanelReopenButton.Click += (_, _) => SetSidePanelVisible(true);

        layersSplitter.SplitterMoved += (_, _) =>
        {
            layout.LayersPanelWidth = layersPanel.Width;
            EditorLayoutSettingsStore.Save(layout);
        };
        sidePanelSplitter.SplitterMoved += (_, _) =>
        {
            layout.SidePanelWidth = sidePanelContainer.Width;
            EditorLayoutSettingsStore.Save(layout);
        };

        SetLayersVisible(layout.LayersPanelVisible);
        SetSidePanelVisible(layout.SidePanelVisible);

        var hRuler = new RulerControl { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        var vRuler = new RulerControl { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };
        var corner = new Panel { Dock = DockStyle.Fill, BackColor = SystemColors.Control };

        var canvasArea = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        canvasArea.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20));
        canvasArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        canvasArea.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        canvasArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        canvasArea.Controls.Add(corner, 0, 0);
        canvasArea.Controls.Add(hRuler, 1, 0);
        canvasArea.Controls.Add(vRuler, 0, 1);
        canvasArea.Controls.Add(_canvas, 1, 1);

        void RefreshPreview()
        {
            if (sidePanelTabs.SelectedTab != previewTab)
            {
                return;
            }

            try
            {
                previewControl.Document = ResolvePreview();
            }
            catch
            {
                previewControl.Document = null;
            }

            previewControl.Invalidate();
        }

        _canvas.DocumentChanged += (_, _) => RefreshPreview();
        sidePanelTabs.SelectedIndexChanged += (_, _) => RefreshPreview();

        var toolStrip = BuildToolStrip(
            out _undoButton,
            out _redoButton,
            out _zoomLabel,
            out var gridToggle,
            out var snapToggle,
            out var guidesToggle,
            out var gridSizeUpDown);

        var pageButton = new ToolStripButton("Página...");
        pageButton.Click += (_, _) => OpenPageSettings();
        toolStrip.Items.Insert(0, pageButton);
        toolStrip.Items.Insert(1, new ToolStripSeparator());

        _canvas.ViewChanged += (_, _) =>
        {
            hRuler.PixelsPerMm = _canvas.PixelsPerMm;
            hRuler.OffsetPx = _canvas.PanOffsetPx.X;
            hRuler.Invalidate();
            vRuler.PixelsPerMm = _canvas.PixelsPerMm;
            vRuler.OffsetPx = _canvas.PanOffsetPx.Y;
            vRuler.Invalidate();
            _zoomLabel.Text = $"{_canvas.ZoomPercent}%";
        };

        Controls.Add(canvasArea);
        Controls.Add(sidePanelContainer);
        Controls.Add(sidePanelSplitter);
        Controls.Add(sidePanelCollapsedStrip);
        Controls.Add(layersPanel);
        Controls.Add(layersSplitter);
        Controls.Add(layersCollapsedStrip);
        Controls.Add(toolStrip);
        Controls.Add(statusBar);

        _canvas.Document = document;
        layersPanel.Canvas = _canvas;
        propertyPanel.Canvas = _canvas;

        // Grid/snap/guides are read from the document now (see LabelCanvasControl.EditorSettings), so
        // the toolbar controls built above — before this document was assigned — reflected only the
        // fallback defaults. Re-sync them to whatever this specific label was actually saved with.
        gridToggle.Checked = _canvas.ShowGrid;
        snapToggle.Checked = _canvas.SnapToGridEnabled;
        guidesToggle.Checked = _canvas.AlignmentGuidesEnabled;
        gridSizeUpDown.Value = (decimal)_canvas.GridSizeMm;
    }

    /// <summary>Resolves the current document (sample data from each variable's default value) for
    /// the "Pré-visualizar" panel — the real rendering pipeline, unlike the canvas's own placeholder
    /// drawing, so what you see there is what Export/Print will actually produce.</summary>
    private ResolvedDocument? ResolvePreview()
    {
        if (_canvas.Document is not { } document)
        {
            return null;
        }

        var sampleData = document.Variables.ToDictionary(
            variable => variable.Name,
            object? (variable) => variable.DefaultValue,
            StringComparer.Ordinal);
        return new LayoutEngine().Resolve(document, new LayoutOptions { SampleData = sampleData });
    }

    private void OpenPageSettings()
    {
        if (_canvas.Document is not { } document)
        {
            return;
        }

        using var dialog = new PageSettingsForm(document.Page);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _canvas.ChangeDocument(doc => doc with { Page = dialog.Result }, "Alterar configurações da página");
    }

    private ToolStrip BuildToolStrip(
        out ToolStripButton undoButton,
        out ToolStripButton redoButton,
        out ToolStripLabel zoomLabel,
        out ToolStripButton gridToggle,
        out ToolStripButton snapToggle,
        out ToolStripButton guidesToggle,
        out NumericUpDown gridSizeUpDown)
    {
        var toolStrip = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };

        if (_onSave is not null)
        {
            var saveButton = new ToolStripButton("Salvar");
            saveButton.Click += (_, _) => Save();
            toolStrip.Items.Add(saveButton);
            toolStrip.Items.Add(new ToolStripSeparator());
        }

        var addButton = new ToolStripDropDownButton("+ Adicionar");
        foreach (var kind in Enum.GetValues<NewElementKind>())
        {
            var item = new ToolStripMenuItem(NewElementFactory.Label(kind));
            item.Click += (_, _) => AddElement(kind);
            addButton.DropDownItems.Add(item);
        }

        toolStrip.Items.Add(addButton);
        toolStrip.Items.Add(new ToolStripSeparator());

        var exportButton = new ToolStripButton("Exportar...");
        exportButton.Click += (_, _) => OpenExportDialog();

        var printButton = new ToolStripButton("Imprimir...");
        printButton.Click += (_, _) => OpenPrintDialog();

        undoButton = new ToolStripButton("Desfazer");
        undoButton.Click += (_, _) => _canvas.Undo();

        redoButton = new ToolStripButton("Refazer");
        redoButton.Click += (_, _) => _canvas.Redo();

        var deleteButton = new ToolStripButton("Excluir");
        deleteButton.Click += (_, _) => _canvas.DeleteSelection();

        var duplicateButton = new ToolStripButton("Duplicar");
        duplicateButton.Click += (_, _) => _canvas.DuplicateSelection();

        var groupButton = new ToolStripButton("Agrupar");
        groupButton.Click += (_, _) => _canvas.GroupSelection();

        var ungroupButton = new ToolStripButton("Desagrupar");
        ungroupButton.Click += (_, _) => _canvas.UngroupSelection();

        // Grid/snap/guides now live on the document's EditorSettings (see LabelCanvasControl), so
        // their setters already trigger a repaint on their own — no need for an extra Invalidate()
        // here. Initial Checked/Value below reflect whatever's loaded by the time this runs; the
        // constructor re-syncs them once the real document is assigned (see SyncEditorSettingsUi).
        var gridToggleLocal = new ToolStripButton("Grade") { CheckOnClick = true, Checked = _canvas.ShowGrid };
        gridToggleLocal.CheckedChanged += (_, _) => _canvas.ShowGrid = gridToggleLocal.Checked;
        gridToggle = gridToggleLocal;

        var snapToggleLocal = new ToolStripButton("Ajustar à grade") { CheckOnClick = true, Checked = _canvas.SnapToGridEnabled };
        snapToggleLocal.CheckedChanged += (_, _) => _canvas.SnapToGridEnabled = snapToggleLocal.Checked;
        snapToggle = snapToggleLocal;

        var guidesToggleLocal = new ToolStripButton("Guias de alinhamento") { CheckOnClick = true, Checked = _canvas.AlignmentGuidesEnabled };
        guidesToggleLocal.CheckedChanged += (_, _) => _canvas.AlignmentGuidesEnabled = guidesToggleLocal.Checked;
        guidesToggle = guidesToggleLocal;

        var gridSizeLabel = new ToolStripLabel("Tam. grade (mm):");
        var gridSizeUpDownLocal = new NumericUpDown
        {
            Minimum = 0.1m,
            Maximum = 50,
            Increment = 0.1m,
            DecimalPlaces = 1,
            Value = (decimal)_canvas.GridSizeMm,
            Width = 55,
        };
        gridSizeUpDownLocal.ValueChanged += (_, _) => _canvas.GridSizeMm = (double)gridSizeUpDownLocal.Value;
        gridSizeUpDown = gridSizeUpDownLocal;

        var zoomOutButton = new ToolStripButton("−");
        zoomOutButton.Click += (_, _) => _canvas.ZoomOut();

        zoomLabel = new ToolStripLabel("100%") { AutoToolTip = false, TextAlign = ContentAlignment.MiddleCenter, Width = 40 };

        var zoomInButton = new ToolStripButton("+");
        zoomInButton.Click += (_, _) => _canvas.ZoomIn();

        var zoomFitButton = new ToolStripButton("Ajustar");
        zoomFitButton.Click += (_, _) => _canvas.ResetZoom();

        toolStrip.Items.AddRange(
        [
            exportButton,
            printButton,
            new ToolStripSeparator(),
            undoButton,
            redoButton,
            new ToolStripSeparator(),
            deleteButton,
            duplicateButton,
            groupButton,
            ungroupButton,
            new ToolStripSeparator(),
            gridToggle,
            snapToggle,
            guidesToggle,
            gridSizeLabel,
            new ToolStripControlHost(gridSizeUpDown),
            new ToolStripSeparator(),
            zoomOutButton,
            zoomLabel,
            zoomInButton,
            zoomFitButton,
        ]);

        return toolStrip;
    }

    private void AddElement(NewElementKind kind)
    {
        if (_canvas.Document is not { } document)
        {
            return;
        }

        var layerId = document.Layers.FirstOrDefault()?.Id;
        var zIndex = document.Elements.Count == 0 ? 0 : document.Elements.Max(e => e.ZIndex) + 1;
        var element = NewElementFactory.Create(kind, _canvas.ViewportCenterMm(), zIndex, layerId);

        // A freshly inserted VariableElement's default expression is a bare identifier ("variavel")
        // that isn't declared anywhere yet — evaluating an undeclared identifier throws and blanks
        // the whole preview (see ElementResolvingVisitor.VisitVariable/Evaluator.ResolveIdentifier),
        // so register a matching document-level variable in the same undo step.
        var newVariables = element is VariableElement variable
            ? new[] { new LabelVariable { Name = variable.Expression, DefaultValue = "Valor" } }
            : null;
        _canvas.AddElement(element, newVariables);
    }

    private void Save()
    {
        if (_canvas.Document is not { } document)
        {
            return;
        }

        _onSave?.Invoke(document);
    }

    private void OpenExportDialog()
    {
        if (_canvas.Document is not { } document)
        {
            return;
        }

        using var dialog = new ExportDialogForm(document);
        dialog.ShowDialog(this);
    }

    private void OpenPrintDialog()
    {
        if (_canvas.Document is not { } document)
        {
            return;
        }

        using var dialog = new PrintDialogForm(document);
        dialog.ShowDialog(this);
    }

    private void UpdateStatus()
    {
        var count = _canvas.SelectedElementIds.Count;
        var selectionText = count switch
        {
            0 => "Nenhuma seleção",
            1 => $"1 elemento selecionado: {_canvas.SelectedElementIds.First()}",
            _ => $"{count} elementos selecionados",
        };

        _statusLabel.Text = $"{selectionText}    |    Undo: {(_canvas.CanUndo ? "disponível" : "—")}    Redo: {(_canvas.CanRedo ? "disponível" : "—")}";
        _undoButton.Enabled = _canvas.CanUndo;
        _redoButton.Enabled = _canvas.CanRedo;
    }

    /// <summary>Called on every <see cref="LabelCanvasControl.LiveChanged"/> tick (every mouse-move of
    /// a drag), so this must stay cheap — just reading already-computed geometry and setting two
    /// NumericUpDown values, never rebuilding controls.</summary>
    private void UpdateGeometryFields()
    {
        if (!_canvas.TryGetSingleSelectionGeometry(out var position, out _))
        {
            _positionXField.Enabled = false;
            _positionYField.Enabled = false;
            return;
        }

        _positionXField.Enabled = true;
        _positionYField.Enabled = true;

        _suppressGeometryFieldEvents = true;
        // Don't clobber a value the user is actively typing into.
        if (!_positionXField.Focused)
        {
            _positionXField.Value = Math.Clamp((decimal)position.X, _positionXField.Minimum, _positionXField.Maximum);
        }

        if (!_positionYField.Focused)
        {
            _positionYField.Value = Math.Clamp((decimal)position.Y, _positionYField.Minimum, _positionYField.Maximum);
        }

        _suppressGeometryFieldEvents = false;
    }
}
