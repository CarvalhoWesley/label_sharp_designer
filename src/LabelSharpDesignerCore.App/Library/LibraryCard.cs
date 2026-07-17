namespace LabelSharpDesignerCore.App.Library;

internal enum LibraryCardAction
{
    Open,
    Duplicate,
    Export,
    Print,
    Delete,
}

/// <summary>One grid tile: thumbnail + name, tapping anywhere opens the editor, and a three-dot menu
/// exposes Editar/Duplicar/Exportar/Imprimir/Excluir. Mirrors the original <c>library_card.dart</c>.</summary>
internal sealed class LibraryCard : Panel
{
    public LibraryEntry Entry { get; }

    public event EventHandler<LibraryCardAction>? ActionRequested;

    public LibraryCard(LibraryEntry entry)
    {
        Entry = entry;

        Width = 220;
        Height = 190;
        Margin = new Padding(8);
        BorderStyle = BorderStyle.FixedSingle;
        Cursor = Cursors.Hand;

        var thumbnail = new PictureBox
        {
            Dock = DockStyle.Top,
            Height = 132,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = SystemColors.ControlDark,
        };

        if (entry.Document.Metadata.ThumbnailPngBase64 is { Length: > 0 } base64)
        {
            try
            {
                using var stream = new MemoryStream(Convert.FromBase64String(base64));
                thumbnail.Image = Image.FromStream(stream);
            }
            catch
            {
                // corrupt/foreign thumbnail data — fall back to the blank placeholder background.
            }
        }

        var nameLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = entry.Document.Name,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            AutoEllipsis = true,
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Editar", null, (_, _) => Raise(LibraryCardAction.Open));
        menu.Items.Add("Duplicar", null, (_, _) => Raise(LibraryCardAction.Duplicate));
        menu.Items.Add("Exportar...", null, (_, _) => Raise(LibraryCardAction.Export));
        menu.Items.Add("Imprimir...", null, (_, _) => Raise(LibraryCardAction.Print));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Excluir", null, (_, _) => Raise(LibraryCardAction.Delete));

        // FlatStyle.System works around a .NET 9 dark-mode bug where the default FlatStyle.Standard
        // renders no button text.
        var menuButton = new Button { Dock = DockStyle.Right, Width = 32, Text = "⋮", FlatStyle = FlatStyle.System };
        menuButton.Click += (_, _) => menu.Show(menuButton, new Point(0, menuButton.Height));

        var bottomRow = new Panel { Dock = DockStyle.Bottom, Height = 34 };
        bottomRow.Controls.Add(nameLabel);
        bottomRow.Controls.Add(menuButton);

        Controls.Add(bottomRow);
        Controls.Add(thumbnail);

        WireOpenOnClick(this, menuButton);
    }

    private void WireOpenOnClick(Control control, Button menuButton)
    {
        if (control == menuButton)
        {
            return;
        }

        control.Click += (_, _) => Raise(LibraryCardAction.Open);
        foreach (Control child in control.Controls)
        {
            WireOpenOnClick(child, menuButton);
        }
    }

    private void Raise(LibraryCardAction action) => ActionRequested?.Invoke(this, action);
}
