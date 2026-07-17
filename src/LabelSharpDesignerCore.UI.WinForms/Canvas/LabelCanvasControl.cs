using System.ComponentModel;
using LabelSharpDesignerCore.Core.Document;
using LabelSharpDesignerCore.Core.Elements;
using LabelSharpDesignerCore.Core.Geometry;
using LabelSharpDesignerCore.History;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace LabelSharpDesignerCore.UI.WinForms.Canvas;

/// <summary>
/// The interactive label editor surface. Draws placeholders straight from the domain model
/// (never the Layout/Rendering pipeline — see <see cref="PlaceholderDrawingVisitor"/>) and
/// supports click/shift-click/marquee selection, drag-move, 8-handle resize + rotate, delete,
/// duplicate, group/ungroup and select-all — all funneled through a <see cref="HistoryManager"/>
/// so every interaction (except live drag feedback) is undoable.
/// </summary>
public sealed class LabelCanvasControl : SKControl
{
    private const float HandleSizePx = 8f;
    private const float RotateHandleOffsetPx = 22f;
    private const float MinElementSizeMm = 1f;
    private const float DuplicateOffsetMm = 5f;
    private const double AlignmentThresholdMm = 1.0;

    /// <summary>"100%" zoom ≈ the page's actual physical size on a 96-DPI screen (1 mm = 96/25.4 px).</summary>
    private const float BasePixelsPerMm = 96f / 25.4f;
    private const float MinPixelsPerMm = BasePixelsPerMm * 0.1f;
    private const float MaxPixelsPerMm = BasePixelsPerMm * 8f;
    private const float ZoomStepFactor = 1.25f;

    private enum InteractionMode
    {
        None,
        Marquee,
        Move,
        Resize,
        Rotate,
    }

    private readonly CanvasTransform _transform = new();
    private readonly HashSet<string> _selectedElementIds = new(StringComparer.Ordinal);

    private HistoryManager? _history;

    private InteractionMode _mode = InteractionMode.None;
    private SKPoint _dragStartMm;
    private SKRect _marqueeRectPx;
    private ResizeHandle _activeHandle = ResizeHandle.None;
    private string? _activeElementId;
    private Dictionary<string, LabelElement> _dragStartElements = new(StringComparer.Ordinal);
    private LabelDocument? _gestureStartDocument;
    private float _rotateStartMouseAngle;
    private double _rotateStartElementRotation;
    private double? _activeGuideXMm;
    private double? _activeGuideYMm;
    private bool _manualZoom;

    /// <summary>Fires only for committed changes (a finished gesture, a property edit, undo/redo) —
    /// never for the many intermediate <see cref="HistoryManager.PreviewChange"/> calls during a
    /// drag/resize/rotate. Expensive listeners (panel rebuilds, preview re-render) should hang off
    /// this event; use <see cref="LiveChanged"/> for cheap per-frame feedback instead.</summary>
    public event EventHandler? DocumentChanged;

    /// <summary>Fires on every document change, including live drag/resize/rotate feedback — keep
    /// handlers cheap (e.g. formatting a status label), never a control rebuild.</summary>
    public event EventHandler? LiveChanged;
    public event EventHandler? SelectionChanged;

    /// <summary>Raised whenever zoom/pan changes (currently only on <see cref="FitToControl"/>), so a
    /// host such as a ruler pair can stay in sync with <see cref="PixelsPerMm"/>/<see cref="PanOffsetPx"/>.</summary>
    public event EventHandler? ViewChanged;

    public LabelCanvasControl()
    {
        DoubleBuffered = true;
    }

    /// <summary>Millimeter size of one grid cell, and the increment element-alignment snaps to. Lives
    /// on the document's <see cref="EditorSettings"/> (see <see cref="UpdateEditorSettings"/>) so it's
    /// saved with the label instead of always resetting to a hardcoded default.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double GridSizeMm
    {
        get => CurrentEditorSettings.GridSizeMm;
        set => UpdateEditorSettings(s => s with { GridSizeMm = value });
    }

    /// <summary>Whether the grid is drawn behind the page content.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowGrid
    {
        get => CurrentEditorSettings.ShowGrid;
        set => UpdateEditorSettings(s => s with { ShowGrid = value });
    }

    /// <summary>Whether dragged/resized elements snap their position to the grid (on axes with no
    /// element-alignment match — see <see cref="AlignmentGuidesEnabled"/>).</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool SnapToGridEnabled
    {
        get => CurrentEditorSettings.SnapToGridEnabled;
        set => UpdateEditorSettings(s => s with { SnapToGridEnabled = value });
    }

    /// <summary>Whether moving a single-axis-unmatched selection snaps to align with other elements'
    /// edges/centers ("smart guides"), drawn as dashed lines while the match is active.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool AlignmentGuidesEnabled
    {
        get => CurrentEditorSettings.AlignmentGuidesEnabled;
        set => UpdateEditorSettings(s => s with { AlignmentGuidesEnabled = value });
    }

    private EditorSettings CurrentEditorSettings => _history?.Current.EditorSettings ?? EditorSettings.Default;

    /// <summary>Updates the document's grid/snap/guide settings directly on <see cref="HistoryManager.Current"/>
    /// via <see cref="HistoryManager.PreviewChange"/> rather than <see cref="ChangeDocument"/> — deliberately
    /// NOT an undo step. Toggling "show grid" and then hitting Undo reverting your last real edit instead
    /// (or reverting the grid toggle itself) would both be surprising; every mainstream design tool treats
    /// view/interaction preferences as outside the undo history even though they're saved with the file.</summary>
    private void UpdateEditorSettings(Func<EditorSettings, EditorSettings> apply)
    {
        if (_history is null)
        {
            return;
        }

        var current = _history.Current;
        var updated = apply(current.EditorSettings);
        if (updated == current.EditorSettings)
        {
            return;
        }

        _history.PreviewChange(current with { EditorSettings = updated });
    }

    public float PixelsPerMm => _transform.PixelsPerMm;

    public PointF PanOffsetPx => new(_transform.PanOffsetPx.X, _transform.PanOffsetPx.Y);

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public LabelDocument? Document
    {
        get => _history?.Current;
        set
        {
            if (_history is not null)
            {
                _history.Changed -= OnHistoryChanged;
            }

            _history = value is null ? null : new HistoryManager(value);
            if (_history is not null)
            {
                _history.Changed += OnHistoryChanged;
            }

            _selectedElementIds.RemoveWhere(id => value?.Elements.Any(e => e.Id == id) != true);
            if (Width > 0 && Height > 0)
            {
                FitToControl();
            }

            Invalidate();
        }
    }

    public IReadOnlyCollection<string> SelectedElementIds => _selectedElementIds;

    public bool CanUndo => _history?.CanUndo ?? false;

    public bool CanRedo => _history?.CanRedo ?? false;

    public void Undo()
    {
        if (_history is null || !_history.CanUndo)
        {
            return;
        }

        _history.Undo();
        PruneSelectionToExistingElements();
    }

    public void Redo()
    {
        if (_history is null || !_history.CanRedo)
        {
            return;
        }

        _history.Redo();
        PruneSelectionToExistingElements();
    }

    public void ClearSelection()
    {
        if (_selectedElementIds.Count == 0)
        {
            return;
        }

        _selectedElementIds.Clear();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    public void SelectAll()
    {
        if (_history is null)
        {
            return;
        }

        _selectedElementIds.Clear();
        foreach (var element in _history.Current.Elements)
        {
            if (IsLayerVisible(element.LayerId) && IsSelectable(element))
            {
                _selectedElementIds.Add(element.Id);
            }
        }

        SelectionChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    public void DeleteSelection()
    {
        if (_history is null || _selectedElementIds.Count == 0)
        {
            return;
        }

        var before = _history.Current;
        var idsToDelete = _selectedElementIds.ToList();
        var after = before with { Elements = before.Elements.Where(e => !idsToDelete.Contains(e.Id)).ToList() };

        _history.Execute(new DeleteCommand { Before = before, After = after, ElementIds = idsToDelete });
        _selectedElementIds.Clear();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void DuplicateSelection()
    {
        if (_history is null || _selectedElementIds.Count == 0)
        {
            return;
        }

        var before = _history.Current;
        var offset = new PointMm(DuplicateOffsetMm, DuplicateOffsetMm);
        var duplicates = before.Elements
            .Where(e => _selectedElementIds.Contains(e.Id))
            .Select(e => CloneWithNewIds(e, offset))
            .ToList();

        var after = before with { Elements = before.Elements.Concat(duplicates).ToList() };
        _history.Execute(new AddCommand { Before = before, After = after, ElementIds = duplicates.Select(d => d.Id).ToList() });

        _selectedElementIds.Clear();
        foreach (var duplicate in duplicates)
        {
            _selectedElementIds.Add(duplicate.Id);
        }

        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void GroupSelection()
    {
        if (_history is null || _selectedElementIds.Count < 2)
        {
            return;
        }

        var before = _history.Current;
        var selected = before.Elements.Where(e => _selectedElementIds.Contains(e.Id)).ToList();
        var bounds = ElementGeometry.CombinedBoundsMm(selected);

        var group = new GroupElement
        {
            Id = Guid.NewGuid().ToString("N"),
            Position = new PointMm(bounds.Left, bounds.Top),
            Size = new SizeMm(bounds.Width, bounds.Height),
            Children = selected,
            ZIndex = selected.Max(e => e.ZIndex),
        };

        var remaining = before.Elements.Where(e => !_selectedElementIds.Contains(e.Id)).ToList();
        remaining.Add(group);
        var after = before with { Elements = remaining };

        _history.Execute(new ChangeDocumentCommand { Before = before, After = after, Reason = "Agrupar elementos" });
        _selectedElementIds.Clear();
        _selectedElementIds.Add(group.Id);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UngroupSelection()
    {
        if (_history is null || _selectedElementIds.Count != 1)
        {
            return;
        }

        var before = _history.Current;
        var selectedId = _selectedElementIds.First();
        if (before.Elements.FirstOrDefault(e => e.Id == selectedId) is not GroupElement group)
        {
            return;
        }

        var remaining = before.Elements.Where(e => e.Id != selectedId).ToList();
        remaining.AddRange(group.Children);
        var after = before with { Elements = remaining };

        _history.Execute(new ChangeDocumentCommand { Before = before, After = after, Reason = "Desagrupar elementos" });
        _selectedElementIds.Clear();
        foreach (var child in group.Children)
        {
            _selectedElementIds.Add(child.Id);
        }

        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void FitToControl()
    {
        if (_history is null)
        {
            return;
        }

        var page = _history.Current.Page;
        _transform.FitToControl((float)page.WidthMm, (float)page.HeightMm, Width, Height);
        ViewChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    /// <summary>Current zoom as a percentage, where 100% is the page's actual physical size on a
    /// 96-DPI screen.</summary>
    public int ZoomPercent => (int)Math.Round(_transform.PixelsPerMm / BasePixelsPerMm * 100f);

    public void ZoomIn() => SetZoom(_transform.PixelsPerMm * ZoomStepFactor, ControlCenterPx());

    public void ZoomOut() => SetZoom(_transform.PixelsPerMm / ZoomStepFactor, ControlCenterPx());

    public void SetZoomPercent(int percent) => SetZoom(BasePixelsPerMm * percent / 100f, ControlCenterPx());

    /// <summary>Returns to auto-fit-to-control zoom/pan, and resumes auto-fitting on future resizes
    /// (undone again the next time the user zooms manually).</summary>
    public void ResetZoom()
    {
        _manualZoom = false;
        FitToControl();
    }

    /// <summary>The document point currently at the center of the visible control — used to place a
    /// newly added element where the user is actually looking rather than at a fixed page position.</summary>
    public PointMm ViewportCenterMm()
    {
        var mm = _transform.PixelsToMm(ControlCenterPx());
        return new PointMm(mm.X, mm.Y);
    }

    /// <summary>Adds a new element (from a toolbar "insert" action, as opposed to duplicating an
    /// existing selection), recorded as a single undo step, and selects it. <paramref name="newVariables"/>
    /// lets the caller register document-level <see cref="LabelVariable"/> declarations in the same
    /// undo step — e.g. a newly inserted <see cref="VariableElement"/>'s default expression needs a
    /// matching declared variable, or evaluating it throws "Unknown variable" and blanks the preview;
    /// any name already present in the document is left untouched.</summary>
    public void AddElement(LabelElement element, IReadOnlyList<LabelVariable>? newVariables = null)
    {
        if (_history is null)
        {
            return;
        }

        var before = _history.Current;
        var variables = newVariables is { Count: > 0 }
            ? before.Variables.Concat(newVariables.Where(v => before.Variables.All(existing => existing.Name != v.Name))).ToList()
            : before.Variables;
        var after = before with { Elements = before.Elements.Concat([element]).ToList(), Variables = variables };
        _history.Execute(new AddCommand { Before = before, After = after, ElementIds = [element.Id] });

        _selectedElementIds.Clear();
        _selectedElementIds.Add(element.Id);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Cheap live readout of the single selected element's position/size — safe to call on
    /// every <see cref="LiveChanged"/> tick (unlike <see cref="Document"/> consumers that rebuild UI),
    /// since it just reads already-computed state.</summary>
    public bool TryGetSingleSelectionGeometry(out PointMm position, out SizeMm size)
    {
        if (_selectedElementIds.Count == 1 && FindElement(_selectedElementIds.First()) is { } element)
        {
            position = element.Position;
            size = element.Size;
            return true;
        }

        position = default;
        size = default;
        return false;
    }

    private SKPoint ControlCenterPx() => new(Width / 2f, Height / 2f);

    private void SetZoom(float pixelsPerMm, SKPoint anchorPx)
    {
        if (_history is null)
        {
            return;
        }

        var clamped = Math.Clamp(pixelsPerMm, MinPixelsPerMm, MaxPixelsPerMm);
        _transform.ZoomAtPoint(clamped, anchorPx);
        _manualZoom = true;
        ViewChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    /// <summary>Applies <paramref name="apply"/> to every selected top-level element, recording the
    /// whole batch as a single undo step. A no-op (every element unchanged, or nothing selected) does
    /// not touch the history stack.</summary>
    public void ApplyPropertyChange(Func<LabelElement, LabelElement> apply, string propertyName)
    {
        if (_history is null || _selectedElementIds.Count == 0)
        {
            return;
        }

        var before = _history.Current;
        var changedIds = new List<string>();
        var afterElements = before.Elements.Select(element =>
        {
            if (!_selectedElementIds.Contains(element.Id))
            {
                return element;
            }

            var updated = apply(element);
            if (!ReferenceEquals(updated, element) && updated != element)
            {
                changedIds.Add(element.Id);
            }

            return updated;
        }).ToList();

        if (changedIds.Count == 0)
        {
            return;
        }

        var after = before with { Elements = afterElements };
        _history.Execute(new ChangePropertyCommand { Before = before, After = after, ElementIds = changedIds, PropertyName = propertyName });
    }

    /// <summary>Applies an arbitrary whole-document edit (layer add/remove/reorder/visibility, and
    /// similar structural changes not covered by a more specific command) as one undo step.</summary>
    public void ChangeDocument(Func<LabelDocument, LabelDocument> apply, string reason)
    {
        if (_history is null)
        {
            return;
        }

        var before = _history.Current;
        var after = apply(before);
        if (ReferenceEquals(after, before) || after == before)
        {
            return;
        }

        _history.Execute(new ChangeDocumentCommand { Before = before, After = after, Reason = reason });
    }

    private bool IsLayerVisible(string? layerId)
    {
        if (layerId is null || _history is null)
        {
            return true;
        }

        var layer = _history.Current.Layers.FirstOrDefault(l => l.Id == layerId);
        return layer is null || layer.Visible;
    }

    private bool IsLayerLocked(string? layerId)
    {
        if (layerId is null || _history is null)
        {
            return false;
        }

        var layer = _history.Current.Layers.FirstOrDefault(l => l.Id == layerId);
        return layer?.Locked ?? false;
    }

    private bool IsSelectable(LabelElement element) => !element.Locked && !IsLayerLocked(element.LayerId);

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Control | Keys.Z:
                Undo();
                return true;
            case Keys.Control | Keys.Y:
                Redo();
                return true;
            case Keys.Delete:
            case Keys.Back:
                DeleteSelection();
                return true;
            case Keys.Control | Keys.D:
                DuplicateSelection();
                return true;
            case Keys.Control | Keys.G:
                GroupSelection();
                return true;
            case Keys.Control | Keys.Shift | Keys.G:
                UngroupSelection();
                return true;
            case Keys.Control | Keys.A:
                SelectAll();
                return true;
            case Keys.Escape:
                ClearSelection();
                return true;
            default:
                return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);
        Draw(e.Surface.Canvas);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);

        if (_manualZoom)
        {
            // Keep the user's chosen zoom level across a resize — just re-center instead of
            // snapping back to fit-to-control.
            if (_history is not null)
            {
                var page = _history.Current.Page;
                _transform.Recenter((float)page.WidthMm, (float)page.HeightMm, Width, Height);
                ViewChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        else
        {
            FitToControl();
        }

        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        if (_history is null || (ModifierKeys & Keys.Control) == 0)
        {
            return;
        }

        var factor = e.Delta > 0 ? ZoomStepFactor : 1f / ZoomStepFactor;
        SetZoom(_transform.PixelsPerMm * factor, new SKPoint(e.X, e.Y));
    }

    private void OnHistoryChanged(object? sender, HistoryChangedEventArgs e)
    {
        LiveChanged?.Invoke(this, EventArgs.Empty);
        if (!e.IsPreview)
        {
            // Only a committed change (gesture finished, property edit, undo/redo) reaches here —
            // this is what expensive listeners (PropertyPanel/LayersPanel full rebuilds, the preview
            // panel's real re-layout+render) subscribe to. Firing this on every mouse-move of a drag
            // was the main cause of choppy dragging: each move was rebuilding every field control in
            // two side panels plus re-running the whole layout engine for the preview.
            DocumentChanged?.Invoke(this, EventArgs.Empty);
        }

        // Update() forces the repaint to happen synchronously right here instead of waiting for the
        // next idle turn of the message loop — without it, drag/resize/rotate's live preview (each
        // mouse-move calls PreviewChange, which raises Changed) can visually lag a full frame behind
        // the mouse, especially over a remote desktop connection, and only "catch up" once movement
        // stops and the queue drains.
        Invalidate();
        Update();
    }

    private void PruneSelectionToExistingElements()
    {
        if (_history is null)
        {
            return;
        }

        var removed = _selectedElementIds.RemoveWhere(id => _history.Current.Elements.All(e => e.Id != id));
        if (removed > 0)
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static LabelElement CloneWithNewIds(LabelElement element, PointMm offset) => element switch
    {
        GroupElement group => group with
        {
            Id = Guid.NewGuid().ToString("N"),
            Position = Offset(group.Position, offset),
            Children = group.Children.Select(child => CloneWithNewIds(child, offset)).ToList(),
        },
        _ => element with
        {
            Id = Guid.NewGuid().ToString("N"),
            Position = Offset(element.Position, offset),
        },
    };

    private static PointMm Offset(PointMm point, PointMm delta) => new(point.X + delta.X, point.Y + delta.Y);

    private void Draw(SKCanvas canvas)
    {
        canvas.Clear(new SKColor(0x3A, 0x3D, 0x41));

        var document = _history?.Current;
        if (document is null)
        {
            return;
        }

        var pageBoundsPx = _transform.BoundsToPixels(SKRect.Create(0, 0, (float)document.Page.WidthMm, (float)document.Page.HeightMm));
        using (var pagePaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill })
        {
            canvas.DrawRect(pageBoundsPx, pagePaint);
        }

        if (ShowGrid)
        {
            DrawGrid(canvas, document, pageBoundsPx);
        }

        var visitor = new PlaceholderDrawingVisitor(canvas, _transform);
        foreach (var element in document.Elements.OrderBy(el => el.ZIndex))
        {
            if (!element.Visible || !IsLayerVisible(element.LayerId))
            {
                continue;
            }

            element.Accept(visitor);
        }

        DrawSelection(canvas, document);

        if (_mode == InteractionMode.Move && (_activeGuideXMm is not null || _activeGuideYMm is not null))
        {
            DrawAlignmentGuides(canvas, document);
        }

        if (_mode == InteractionMode.Marquee)
        {
            using var marqueeFill = new SKPaint { Color = new SKColor(64, 128, 255, 40), Style = SKPaintStyle.Fill };
            using var marqueeBorder = new SKPaint { Color = new SKColor(64, 128, 255), Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
            canvas.DrawRect(_marqueeRectPx, marqueeFill);
            canvas.DrawRect(_marqueeRectPx, marqueeBorder);
        }
    }

    private void DrawGrid(SKCanvas canvas, LabelDocument document, SKRect pageBoundsPx)
    {
        if (GridSizeMm <= 0)
        {
            return;
        }

        using var gridPaint = new SKPaint { Color = new SKColor(0, 0, 0, 30), Style = SKPaintStyle.Stroke, StrokeWidth = 1 };

        for (var x = 0.0; x <= document.Page.WidthMm + 0.001; x += GridSizeMm)
        {
            var xPx = _transform.MmToPixels(new SKPoint((float)x, 0)).X;
            canvas.DrawLine(xPx, pageBoundsPx.Top, xPx, pageBoundsPx.Bottom, gridPaint);
        }

        for (var y = 0.0; y <= document.Page.HeightMm + 0.001; y += GridSizeMm)
        {
            var yPx = _transform.MmToPixels(new SKPoint(0, (float)y)).Y;
            canvas.DrawLine(pageBoundsPx.Left, yPx, pageBoundsPx.Right, yPx, gridPaint);
        }
    }

    private void DrawAlignmentGuides(SKCanvas canvas, LabelDocument document)
    {
        using var guidePaint = new SKPaint { Color = new SKColor(255, 64, 128), Style = SKPaintStyle.Stroke, StrokeWidth = 1, PathEffect = SKPathEffect.CreateDash([4, 3], 0) };
        var pageHeightPx = (float)(document.Page.HeightMm * _transform.PixelsPerMm) + _transform.PanOffsetPx.Y;
        var pageWidthPx = (float)(document.Page.WidthMm * _transform.PixelsPerMm) + _transform.PanOffsetPx.X;

        if (_activeGuideXMm is { } guideX)
        {
            var xPx = _transform.MmToPixels(new SKPoint((float)guideX, 0)).X;
            canvas.DrawLine(xPx, 0, xPx, pageHeightPx, guidePaint);
        }

        if (_activeGuideYMm is { } guideY)
        {
            var yPx = _transform.MmToPixels(new SKPoint(0, (float)guideY)).Y;
            canvas.DrawLine(0, yPx, pageWidthPx, yPx, guidePaint);
        }
    }

    private void DrawSelection(SKCanvas canvas, LabelDocument document)
    {
        if (_selectedElementIds.Count == 0)
        {
            return;
        }

        var selectedElements = document.Elements.Where(el => _selectedElementIds.Contains(el.Id)).ToList();
        using var outlinePaint = new SKPaint { Color = new SKColor(30, 120, 255), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };

        foreach (var element in selectedElements)
        {
            var cornersPx = Array.ConvertAll(ElementGeometry.RotatedCorners(element), _transform.MmToPixels);
            for (var i = 0; i < cornersPx.Length; i++)
            {
                var next = cornersPx[(i + 1) % cornersPx.Length];
                canvas.DrawLine(cornersPx[i], next, outlinePaint);
            }
        }

        if (selectedElements.Count == 1)
        {
            DrawHandles(canvas, selectedElements[0]);
        }
    }

    private void DrawHandles(SKCanvas canvas, LabelElement element)
    {
        using var handleFill = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = true };
        using var handleBorder = new SKPaint { Color = new SKColor(30, 120, 255), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };

        foreach (var (handle, positionPx) in GetHandlePositionsPx(element))
        {
            if (handle == ResizeHandle.Rotate)
            {
                canvas.DrawCircle(positionPx, HandleSizePx / 2, handleFill);
                canvas.DrawCircle(positionPx, HandleSizePx / 2, handleBorder);
                continue;
            }

            var rect = SKRect.Create(positionPx.X - HandleSizePx / 2, positionPx.Y - HandleSizePx / 2, HandleSizePx, HandleSizePx);
            canvas.DrawRect(rect, handleFill);
            canvas.DrawRect(rect, handleBorder);
        }
    }

    private Dictionary<ResizeHandle, SKPoint> GetHandlePositionsPx(LabelElement element)
    {
        var corners = ElementGeometry.RotatedCorners(element); // NW, NE, SE, SW
        var nw = corners[0];
        var ne = corners[1];
        var se = corners[2];
        var sw = corners[3];

        var localUpMm = new SKPoint(0, -RotateHandleOffsetPx / _transform.PixelsPerMm);
        var rotateLocalMm = new SKPoint(ElementGeometry.CenterMm(element).X, ElementGeometry.BoundsMm(element).Top + localUpMm.Y);
        var rotateHandleMm = ElementGeometry.RotatePoint(rotateLocalMm, ElementGeometry.CenterMm(element), element.RotationDegrees);

        return new Dictionary<ResizeHandle, SKPoint>
        {
            [ResizeHandle.NW] = _transform.MmToPixels(nw),
            [ResizeHandle.NE] = _transform.MmToPixels(ne),
            [ResizeHandle.SE] = _transform.MmToPixels(se),
            [ResizeHandle.SW] = _transform.MmToPixels(sw),
            [ResizeHandle.N] = _transform.MmToPixels(Midpoint(nw, ne)),
            [ResizeHandle.E] = _transform.MmToPixels(Midpoint(ne, se)),
            [ResizeHandle.S] = _transform.MmToPixels(Midpoint(se, sw)),
            [ResizeHandle.W] = _transform.MmToPixels(Midpoint(sw, nw)),
            [ResizeHandle.Rotate] = _transform.MmToPixels(rotateHandleMm),
        };
    }

    private static SKPoint Midpoint(SKPoint a, SKPoint b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        if (_history is null || e.Button != MouseButtons.Left)
        {
            return;
        }

        var mousePx = new SKPoint(e.X, e.Y);
        var mouseMm = _transform.PixelsToMm(mousePx);

        if (_selectedElementIds.Count == 1 && FindElement(_selectedElementIds.First()) is { } singleSelected)
        {
            var handle = HitTestHandle(singleSelected, mousePx);
            if (handle == ResizeHandle.Rotate)
            {
                BeginRotate(singleSelected, mousePx);
                return;
            }

            if (handle != ResizeHandle.None)
            {
                BeginResize(singleSelected, handle, mouseMm);
                return;
            }
        }

        var hit = HitTestTopmost(mouseMm);
        var modifierHeld = (ModifierKeys & (Keys.Shift | Keys.Control)) != 0;

        if (hit is null)
        {
            if (!modifierHeld)
            {
                ClearSelection();
            }

            _mode = InteractionMode.Marquee;
            _dragStartMm = mouseMm;
            _marqueeRectPx = SKRect.Create(mousePx.X, mousePx.Y, 0, 0);
            return;
        }

        if (modifierHeld)
        {
            if (!_selectedElementIds.Remove(hit.Id))
            {
                _selectedElementIds.Add(hit.Id);
            }

            SelectionChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
            return;
        }

        if (!_selectedElementIds.Contains(hit.Id))
        {
            _selectedElementIds.Clear();
            _selectedElementIds.Add(hit.Id);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        BeginMove(mouseMm);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_history is null)
        {
            return;
        }

        var mousePx = new SKPoint(e.X, e.Y);
        var mouseMm = _transform.PixelsToMm(mousePx);

        switch (_mode)
        {
            case InteractionMode.Marquee:
                var dragStartPx = _transform.MmToPixels(_dragStartMm);
                _marqueeRectPx = SKRect.Create(
                    Math.Min(dragStartPx.X, mousePx.X),
                    Math.Min(dragStartPx.Y, mousePx.Y),
                    Math.Abs(mousePx.X - dragStartPx.X),
                    Math.Abs(mousePx.Y - dragStartPx.Y));
                Invalidate();
                Update();
                break;

            case InteractionMode.Move:
                ApplyMove(mouseMm);
                break;

            case InteractionMode.Resize:
                ApplyResize(mouseMm);
                break;

            case InteractionMode.Rotate:
                ApplyRotate(mousePx);
                break;
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        switch (_mode)
        {
            case InteractionMode.Marquee:
                CompleteMarquee();
                break;

            case InteractionMode.Move:
                CommitIfChanged(new MoveCommand
                {
                    Before = _gestureStartDocument!,
                    After = _history!.Current,
                    ElementIds = _dragStartElements.Keys.ToList(),
                });
                break;

            case InteractionMode.Resize:
                CommitIfChanged(new ResizeCommand
                {
                    Before = _gestureStartDocument!,
                    After = _history!.Current,
                    ElementId = _activeElementId!,
                });
                break;

            case InteractionMode.Rotate:
                CommitIfChanged(new RotateCommand
                {
                    Before = _gestureStartDocument!,
                    After = _history!.Current,
                    ElementId = _activeElementId!,
                });
                break;
        }

        _mode = InteractionMode.None;
        _activeHandle = ResizeHandle.None;
        _activeElementId = null;
        _gestureStartDocument = null;
        _dragStartElements = new Dictionary<string, LabelElement>(StringComparer.Ordinal);
        _activeGuideXMm = null;
        _activeGuideYMm = null;
        Invalidate();
    }

    private void CommitIfChanged(DocumentCommand command)
    {
        if (_gestureStartDocument is null || ReferenceEquals(command.Before, command.After))
        {
            return;
        }

        // Current already equals command.After from the gesture's live preview; Execute just
        // records the Before -> After transition as one undo step without changing Current.
        _history!.Execute(command);
    }

    private void CompleteMarquee()
    {
        if (_history is null)
        {
            return;
        }

        var marqueeMm = SKRect.Create(
            _transform.PixelsToMm(new SKPoint(_marqueeRectPx.Left, _marqueeRectPx.Top)),
            new SKSize(_marqueeRectPx.Width / _transform.PixelsPerMm, _marqueeRectPx.Height / _transform.PixelsPerMm));

        if (marqueeMm.Width < 0.5f && marqueeMm.Height < 0.5f)
        {
            return;
        }

        _selectedElementIds.Clear();
        foreach (var element in _history.Current.Elements)
        {
            if (!IsLayerVisible(element.LayerId) || !IsSelectable(element))
            {
                continue;
            }

            var corners = ElementGeometry.RotatedCorners(element);
            if (corners.All(marqueeMm.Contains))
            {
                _selectedElementIds.Add(element.Id);
            }
        }

        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void BeginMove(SKPoint mouseMm)
    {
        _mode = InteractionMode.Move;
        _dragStartMm = mouseMm;
        _gestureStartDocument = _history!.Current;
        _dragStartElements = _history.Current.Elements
            .Where(el => _selectedElementIds.Contains(el.Id))
            .ToDictionary(el => el.Id, el => el, StringComparer.Ordinal);
    }

    private void ApplyMove(SKPoint mouseMm)
    {
        var deltaX = mouseMm.X - _dragStartMm.X;
        var deltaY = mouseMm.Y - _dragStartMm.Y;

        var rawBounds = ElementGeometry.CombinedBoundsMm(_dragStartElements.Values);
        var movingBounds = new ElementBoundsMm(rawBounds.Left + deltaX, rawBounds.Top + deltaY, rawBounds.Width, rawBounds.Height);

        double snappedLeft = movingBounds.Left;
        double snappedTop = movingBounds.Top;
        double? guideX = null;
        double? guideY = null;

        if (AlignmentGuidesEnabled && _history is not null)
        {
            var others = _history.Current.Elements
                .Where(el => !_selectedElementIds.Contains(el.Id))
                .Select(el =>
                {
                    var bounds = ElementGeometry.BoundsMm(el);
                    return new ElementBoundsMm(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
                })
                .ToList();

            var result = AlignmentSnap.SnapToElements(movingBounds, others, AlignmentThresholdMm);
            snappedLeft = result.AdjustedLeft;
            snappedTop = result.AdjustedTop;
            guideX = result.GuideX;
            guideY = result.GuideY;
        }

        if (SnapToGridEnabled)
        {
            if (guideX is null)
            {
                snappedLeft = AlignmentSnap.SnapToGrid(snappedLeft, GridSizeMm);
            }

            if (guideY is null)
            {
                snappedTop = AlignmentSnap.SnapToGrid(snappedTop, GridSizeMm);
            }
        }

        _activeGuideXMm = guideX;
        _activeGuideYMm = guideY;

        var finalDeltaX = (float)(snappedLeft - rawBounds.Left);
        var finalDeltaY = (float)(snappedTop - rawBounds.Top);

        PreviewElements(_dragStartElements.Values.Select(original => original with
        {
            Position = new PointMm(original.Position.X + finalDeltaX, original.Position.Y + finalDeltaY),
        }));
    }

    private void BeginResize(LabelElement element, ResizeHandle handle, SKPoint mouseMm)
    {
        _mode = InteractionMode.Resize;
        _activeHandle = handle;
        _activeElementId = element.Id;
        _gestureStartDocument = _history!.Current;
        _dragStartElements = new Dictionary<string, LabelElement> { [element.Id] = element };
    }

    private void ApplyResize(SKPoint mouseMm)
    {
        if (_activeElementId is null || !_dragStartElements.TryGetValue(_activeElementId, out var original))
        {
            return;
        }

        var center = ElementGeometry.CenterMm(original);
        var localPoint = original.RotationDegrees == 0
            ? mouseMm
            : ElementGeometry.RotatePoint(mouseMm, center, -original.RotationDegrees);

        // Snapping the dragged handle's own local point to the grid is only exact for
        // unrotated elements; for rotated ones it's a reasonable approximation (see the
        // corner-drift note below, which already accepts imprecision under rotation).
        if (SnapToGridEnabled)
        {
            localPoint = new SKPoint(
                (float)AlignmentSnap.SnapToGrid(localPoint.X, GridSizeMm),
                (float)AlignmentSnap.SnapToGrid(localPoint.Y, GridSizeMm));
        }

        var bounds = ElementGeometry.BoundsMm(original);
        float left = bounds.Left, top = bounds.Top, right = bounds.Right, bottom = bounds.Bottom;

        switch (_activeHandle)
        {
            case ResizeHandle.NW: left = localPoint.X; top = localPoint.Y; break;
            case ResizeHandle.N: top = localPoint.Y; break;
            case ResizeHandle.NE: right = localPoint.X; top = localPoint.Y; break;
            case ResizeHandle.E: right = localPoint.X; break;
            case ResizeHandle.SE: right = localPoint.X; bottom = localPoint.Y; break;
            case ResizeHandle.S: bottom = localPoint.Y; break;
            case ResizeHandle.SW: left = localPoint.X; bottom = localPoint.Y; break;
            case ResizeHandle.W: left = localPoint.X; break;
        }

        if (right - left < MinElementSizeMm)
        {
            if (_activeHandle is ResizeHandle.NW or ResizeHandle.W or ResizeHandle.SW)
            {
                left = right - MinElementSizeMm;
            }
            else
            {
                right = left + MinElementSizeMm;
            }
        }

        if (bottom - top < MinElementSizeMm)
        {
            if (_activeHandle is ResizeHandle.NW or ResizeHandle.N or ResizeHandle.NE)
            {
                top = bottom - MinElementSizeMm;
            }
            else
            {
                bottom = top + MinElementSizeMm;
            }
        }

        // Note: for rotated elements the corner opposite the dragged handle is computed in the
        // element's local (unrotated) frame and will visually drift slightly during the drag —
        // a correct fixed-anchor solve under rotation is out of scope for this pass.
        PreviewElements([original with
        {
            Position = new PointMm(left, top),
            Size = new SizeMm(right - left, bottom - top),
        }]);
    }

    private void BeginRotate(LabelElement element, SKPoint mousePx)
    {
        _mode = InteractionMode.Rotate;
        _activeElementId = element.Id;
        _gestureStartDocument = _history!.Current;
        _dragStartElements = new Dictionary<string, LabelElement> { [element.Id] = element };
        _rotateStartElementRotation = element.RotationDegrees;
        _rotateStartMouseAngle = AngleFromCenterPx(element, mousePx);
    }

    private void ApplyRotate(SKPoint mousePx)
    {
        if (_activeElementId is null || !_dragStartElements.TryGetValue(_activeElementId, out var original))
        {
            return;
        }

        var currentAngle = AngleFromCenterPx(original, mousePx);
        var newRotation = _rotateStartElementRotation + (currentAngle - _rotateStartMouseAngle);

        PreviewElements([original with { RotationDegrees = newRotation }]);
    }

    private float AngleFromCenterPx(LabelElement element, SKPoint mousePx)
    {
        var centerPx = _transform.MmToPixels(ElementGeometry.CenterMm(element));
        return (float)(Math.Atan2(mousePx.Y - centerPx.Y, mousePx.X - centerPx.X) * 180 / Math.PI);
    }

    private LabelElement? FindElement(string id) => _history?.Current.Elements.FirstOrDefault(e => e.Id == id);

    private LabelElement? HitTestTopmost(SKPoint mouseMm) => _history?.Current.Elements
        .Where(el => el.Visible && IsLayerVisible(el.LayerId) && IsSelectable(el))
        .OrderByDescending(el => el.ZIndex)
        .FirstOrDefault(el => ElementGeometry.HitTest(el, mouseMm));

    private ResizeHandle HitTestHandle(LabelElement element, SKPoint mousePx)
    {
        foreach (var (handle, positionPx) in GetHandlePositionsPx(element))
        {
            var radius = handle == ResizeHandle.Rotate ? HandleSizePx : HandleSizePx * 0.75f;
            if (Distance(mousePx, positionPx) <= radius)
            {
                return handle;
            }
        }

        return ResizeHandle.None;
    }

    private static float Distance(SKPoint a, SKPoint b) => (float)Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    /// <summary>Live-feedback mutation during an in-progress drag/resize/rotate gesture — does not
    /// touch the undo stack. The gesture's final state is committed as one step in OnMouseUp.</summary>
    private void PreviewElements(IEnumerable<LabelElement> updated)
    {
        if (_history is null)
        {
            return;
        }

        var updatedById = updated.ToDictionary(el => el.Id, el => el, StringComparer.Ordinal);
        var next = _history.Current with
        {
            Elements = _history.Current.Elements.Select(el => updatedById.TryGetValue(el.Id, out var replacement) ? replacement : el).ToList(),
        };

        _history.PreviewChange(next);
    }
}
