using System.ComponentModel;
using LabelSharpDesignerCore.Core.Document;
using LabelSharpDesignerCore.UI.WinForms.Canvas;

namespace LabelSharpDesignerCore.UI.WinForms.Panels;

/// <summary>
/// Lists a document's <see cref="LabelLayer"/>s: visibility/lock toggles, up/down reordering,
/// and create/delete — mirrors the original Flutter <c>layers_panel.dart</c>. Every edit goes
/// through <see cref="LabelCanvasControl.ChangeDocument"/>, so it is undoable like any other
/// canvas edit; this panel never mutates a <see cref="LabelDocument"/> directly.
/// </summary>
public sealed class LayersPanel : UserControl
{
    private const int RowHeight = 46;

    private readonly Panel _list;
    private LabelCanvasControl? _canvas;

    public LayersPanel()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 28 };
        var titleLabel = new Label { Text = "Camadas", AutoSize = true, Location = new System.Drawing.Point(6, 6), Font = new Font(Font, FontStyle.Bold) };
        // FlatStyle.System works around a .NET 9 dark-mode bug where the default FlatStyle.Standard
        // renders no button text.
        // Added to the header before addButton: for controls sharing the same Dock edge, whichever is
        // added LATER ends up closer to the container's center, so this order puts closeButton at the
        // outermost/rightmost position (like a docked tool window's own close "x" in Visual Studio)
        // with addButton just to its left.
        var closeButton = new Button { Text = "✕", Width = 24, Height = 22, Dock = DockStyle.Right, FlatStyle = FlatStyle.System };
        closeButton.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        var addButton = new Button { Text = "+ Nova", Width = 60, Height = 22, Dock = DockStyle.Right, FlatStyle = FlatStyle.System };
        addButton.Click += (_, _) => AddLayer();
        header.Controls.Add(titleLabel);
        header.Controls.Add(closeButton);
        header.Controls.Add(addButton);

        _list = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        _list.Resize += (_, _) => Rebuild();

        Controls.Add(_list);
        Controls.Add(header);
    }

    /// <summary>Raised when the header's own close button is clicked — the host (EditorForm) owns
    /// actually hiding this panel and showing a way to bring it back, mirroring how a Visual Studio
    /// docked tool window's own close button doesn't destroy the window, just collapses it.</summary>
    public event EventHandler? CloseRequested;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public LabelCanvasControl? Canvas
    {
        get => _canvas;
        set
        {
            if (_canvas is not null)
            {
                _canvas.DocumentChanged -= OnDocumentChanged;
            }

            _canvas = value;
            if (_canvas is not null)
            {
                _canvas.DocumentChanged += OnDocumentChanged;
            }

            Rebuild();
        }
    }

    /// <summary>Deferred via <see cref="Control.BeginInvoke(Delegate)"/>: <see cref="Rebuild"/> disposes
    /// every row control, and this handler can itself be running inside one of those controls' own
    /// event (e.g. a checkbox's <c>CheckedChanged</c> triggering <c>ChangeDocument</c>) — rebuilding
    /// synchronously would dispose a control while its own handler is still on the call stack.</summary>
    private void OnDocumentChanged(object? sender, EventArgs e)
    {
        if (IsHandleCreated)
        {
            BeginInvoke(new MethodInvoker(Rebuild));
        }
        else
        {
            Rebuild();
        }
    }

    private void AddLayer()
    {
        var document = _canvas?.Document;
        if (document is null)
        {
            return;
        }

        var nextOrder = document.Layers.Count == 0 ? 0 : document.Layers.Max(l => l.Order) + 1;
        var newLayer = new LabelLayer { Id = Guid.NewGuid().ToString("N"), Name = $"Camada {document.Layers.Count + 1}", Order = nextOrder };
        _canvas!.ChangeDocument(doc => doc with { Layers = doc.Layers.Append(newLayer).ToList() }, "Adicionar camada");
    }

    private void RemoveLayer(LabelLayer layer)
    {
        var document = _canvas?.Document;
        if (document is null)
        {
            return;
        }

        if (document.Layers.Count <= 1)
        {
            MessageBox.Show(this, "Não é possível excluir a única camada.", "Excluir camada", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var elementCount = document.Elements.Count(e => e.LayerId == layer.Id);
        if (elementCount > 0)
        {
            var message = $"A camada \"{layer.Name}\" tem {elementCount} elemento(s). Excluir a camada também exclui esses elementos.";
            if (MessageBox.Show(this, message, "Excluir camada", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
            {
                return;
            }
        }

        _canvas!.ChangeDocument(doc => doc with
        {
            Layers = doc.Layers.Where(l => l.Id != layer.Id).ToList(),
            Elements = doc.Elements.Where(e => e.LayerId != layer.Id).ToList(),
        }, "Excluir camada");
    }

    private void ToggleVisible(LabelLayer layer) => _canvas?.ChangeDocument(
        doc => doc with { Layers = doc.Layers.Select(l => l.Id == layer.Id ? l with { Visible = !l.Visible } : l).ToList() },
        "Alternar visibilidade da camada");

    private void ToggleLocked(LabelLayer layer) => _canvas?.ChangeDocument(
        doc => doc with { Layers = doc.Layers.Select(l => l.Id == layer.Id ? l with { Locked = !l.Locked } : l).ToList() },
        "Alternar bloqueio da camada");

    private void MoveLayer(LabelLayer layer, int direction)
    {
        var document = _canvas?.Document;
        if (document is null)
        {
            return;
        }

        var ordered = document.Layers.OrderBy(l => l.Order).ToList();
        var index = ordered.FindIndex(l => l.Id == layer.Id);
        var swapIndex = index + direction;
        if (index < 0 || swapIndex < 0 || swapIndex >= ordered.Count)
        {
            return;
        }

        var a = ordered[index];
        var b = ordered[swapIndex];
        _canvas!.ChangeDocument(doc => doc with
        {
            Layers = doc.Layers.Select(l => l.Id == a.Id ? l with { Order = b.Order } : l.Id == b.Id ? l with { Order = a.Order } : l).ToList(),
        }, "Reordenar camadas");
    }

    private void Rebuild()
    {
        _list.SuspendLayout();
        foreach (Control control in _list.Controls)
        {
            control.Dispose();
        }

        _list.Controls.Clear();

        var document = _canvas?.Document;
        if (document is null)
        {
            _list.ResumeLayout();
            return;
        }

        var ordered = document.Layers.OrderBy(l => l.Order).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var row = BuildRow(ordered[i], i, ordered.Count, document);
            row.Top = i * RowHeight;
            _list.Controls.Add(row);
        }

        _list.ResumeLayout();
    }

    private Panel BuildRow(LabelLayer layer, int index, int count, LabelDocument document)
    {
        var elementCount = document.Elements.Count(e => e.LayerId == layer.Id);
        var scrollbarAllowancePx = 24;

        var row = new Panel
        {
            Left = 0,
            Width = Math.Max(_list.ClientSize.Width - scrollbarAllowancePx, 120),
            Height = RowHeight - 2,
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
            BorderStyle = BorderStyle.FixedSingle,
        };

        var nameLabel = new Label
        {
            Text = elementCount == 1 ? $"{layer.Name} (1 elemento)" : $"{layer.Name} ({elementCount} elementos)",
            Left = 4,
            Top = 4,
            Width = row.Width - 70,
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
            AutoEllipsis = true,
        };

        var upButton = new Button { Text = "▲", Width = 20, Height = 20, Anchor = AnchorStyles.Top | AnchorStyles.Right, Enabled = index > 0, FlatStyle = FlatStyle.System };
        var downButton = new Button { Text = "▼", Width = 20, Height = 20, Anchor = AnchorStyles.Top | AnchorStyles.Right, Enabled = index < count - 1, FlatStyle = FlatStyle.System };
        var deleteButton = new Button { Text = "✕", Width = 20, Height = 20, Anchor = AnchorStyles.Top | AnchorStyles.Right, FlatStyle = FlatStyle.System };
        deleteButton.Left = row.Width - deleteButton.Width - 2;
        downButton.Left = deleteButton.Left - downButton.Width - 2;
        upButton.Left = downButton.Left - upButton.Width - 2;
        deleteButton.Top = downButton.Top = upButton.Top = 2;

        upButton.Click += (_, _) => MoveLayer(layer, -1);
        downButton.Click += (_, _) => MoveLayer(layer, 1);
        deleteButton.Click += (_, _) => RemoveLayer(layer);

        var visibleCheck = new CheckBox { Text = "Visível", Checked = layer.Visible, Left = 4, Top = 24, Width = 70, AutoSize = false };
        visibleCheck.CheckedChanged += (_, _) => ToggleVisible(layer);

        var lockedCheck = new CheckBox { Text = "Bloqueada", Checked = layer.Locked, Left = 78, Top = 24, Width = 100, AutoSize = false };
        lockedCheck.CheckedChanged += (_, _) => ToggleLocked(layer);

        row.Controls.Add(nameLabel);
        row.Controls.Add(upButton);
        row.Controls.Add(downButton);
        row.Controls.Add(deleteButton);
        row.Controls.Add(visibleCheck);
        row.Controls.Add(lockedCheck);

        return row;
    }
}
