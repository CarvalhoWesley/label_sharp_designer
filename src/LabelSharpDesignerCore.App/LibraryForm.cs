using LabelSharpDesignerCore.App.Library;

namespace LabelSharpDesignerCore.App;

/// <summary>
/// Home screen: a grid of saved <c>.label</c> documents with new/open/duplicate/delete/export/print
/// actions, an empty-state CTA, and a light/dark/system theme switcher. Mirrors the original Flutter
/// <c>library_screen.dart</c>. Persistence is always explicit — the editor only writes to disk when
/// its own Save button fires the callback passed into it here; nothing in this form or the editor
/// autosaves.
/// </summary>
public sealed class LibraryForm : Form
{
    private readonly LibraryRepository _repository;
    private readonly AppThemeMode _currentThemeMode;
    private readonly IReadOnlyCollection<NewElementKind>? _allowedElementKinds;
    private readonly bool _showLayersPanel;
    private readonly FlowLayoutPanel _grid;
    private readonly Panel _emptyState;

    /// <summary>Set (and the form closed) when the user asks to switch themes — <c>Program.cs</c>
    /// reads this after <see cref="Application.Run(Form)"/> returns and, if non-null, applies the new
    /// <see cref="System.Windows.Forms.SystemColorMode"/> and opens a fresh <see cref="LibraryForm"/>.
    /// Themes can't be repainted live onto already-created controls, so switching always means
    /// recreating the form rather than restyling this one in place.</summary>
    public AppThemeMode? RequestedThemeMode { get; private set; }

    /// <param name="allowedElementKinds">Forwarded as-is to every <see cref="EditorForm"/> this library
    /// opens (see that constructor's own doc) — <see langword="null"/> or empty offers every element
    /// type, same default as the editor itself.</param>
    /// <param name="showLayersPanel">Forwarded as-is to every <see cref="EditorForm"/> this library
    /// opens (see that constructor's own doc) — defaults to <see langword="true"/>, same as the editor
    /// itself.</param>
    public LibraryForm(LibraryRepository repository, AppThemeMode currentThemeMode = AppThemeMode.System, IReadOnlyCollection<NewElementKind>? allowedElementKinds = null, bool showLayersPanel = true)
    {
        _repository = repository;
        _currentThemeMode = currentThemeMode;
        _allowedElementKinds = allowedElementKinds;
        _showLayersPanel = showLayersPanel;

        Text = "LabelSharpDesignerCore — Biblioteca";
        Width = 1000;
        Height = 720;
        StartPosition = FormStartPosition.CenterScreen;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(8, 6, 8, 6),
        };

        // FlatStyle.System everywhere in this form works around a .NET 9 dark-mode bug where the
        // default FlatStyle.Standard renders no button text.
        var newButton = new Button { Text = "+ Nova etiqueta", AutoSize = true, FlatStyle = FlatStyle.System };
        newButton.Click += (_, _) => CreateAndOpen();
        toolbar.Controls.Add(newButton);

        var themeButton = new Button { Text = _currentThemeMode.Label(), AutoSize = true, Margin = new Padding(16, 3, 3, 3), FlatStyle = FlatStyle.System };
        themeButton.Click += (_, _) =>
        {
            RequestedThemeMode = _currentThemeMode.Next();
            Close();
        };
        toolbar.Controls.Add(themeButton);

        _grid = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(8),
        };

        _emptyState = BuildEmptyState();

        Controls.Add(_grid);
        Controls.Add(_emptyState);
        Controls.Add(toolbar);

        Refresh_();
    }

    private Panel BuildEmptyState()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Visible = false };

        var content = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            Anchor = AnchorStyles.None,
        };

        var icon = new Label
        {
            Text = "🏷",
            Font = new Font(Font.FontFamily, 36f),
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        var message = new Label
        {
            Text = "Nenhuma etiqueta ainda.",
            Font = new Font(Font.FontFamily, 11f),
            ForeColor = SystemColors.GrayText,
            AutoSize = true,
        };
        var ctaButton = new Button { Text = "+ Nova etiqueta", AutoSize = true, FlatStyle = FlatStyle.System };
        ctaButton.Click += (_, _) => CreateAndOpen();

        content.Controls.Add(icon);
        content.Controls.Add(message);
        content.Controls.Add(ctaButton);
        panel.Controls.Add(content);

        panel.Resize += (_, _) => CenterContent(panel, content);
        content.SizeChanged += (_, _) => CenterContent(panel, content);

        return panel;
    }

    private static void CenterContent(Panel panel, Control content)
    {
        content.Left = Math.Max(0, (panel.ClientSize.Width - content.Width) / 2);
        content.Top = Math.Max(0, (panel.ClientSize.Height - content.Height) / 2);
    }

    private void Refresh_()
    {
        foreach (Control control in _grid.Controls)
        {
            control.Dispose();
        }

        _grid.Controls.Clear();

        var entries = _repository.List();
        foreach (var entry in entries)
        {
            var card = new LibraryCard(entry);
            card.ActionRequested += (_, action) => HandleCardAction(entry, action);
            _grid.Controls.Add(card);
        }

        _emptyState.Visible = entries.Count == 0;
        _grid.Visible = entries.Count > 0;
    }

    private void HandleCardAction(LibraryEntry entry, LibraryCardAction action)
    {
        switch (action)
        {
            case LibraryCardAction.Open:
                OpenEditor(entry);
                break;
            case LibraryCardAction.Duplicate:
                _repository.Duplicate(entry);
                Refresh_();
                break;
            case LibraryCardAction.Export:
                using (var dialog = new ExportDialogForm(entry.Document))
                {
                    dialog.ShowDialog(this);
                }

                break;
            case LibraryCardAction.Print:
                using (var dialog = new PrintDialogForm(entry.Document))
                {
                    dialog.ShowDialog(this);
                }

                break;
            case LibraryCardAction.Delete:
                DeleteWithConfirmation(entry);
                break;
        }
    }

    private void DeleteWithConfirmation(LibraryEntry entry)
    {
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

    /// <summary>Asks for the page size/DPI/orientation/margins up front — mirrors the original Flutter
    /// project's new-label flow — instead of dropping straight into the editor at a fixed default
    /// size and making the user find the "Página..." button afterward.</summary>
    private void CreateAndOpen()
    {
        using var dialog = new PageSettingsForm(LibraryRepository.DefaultPage);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var entry = _repository.Create(dialog.Result);
        OpenEditor(entry);
    }

    private void OpenEditor(LibraryEntry entry)
    {
        var current = entry;
        using var editor = new EditorForm(current.Document, document => current = _repository.Save(current, document), _allowedElementKinds, _showLayersPanel);
        editor.ShowDialog(this);
        Refresh_();
    }
}
