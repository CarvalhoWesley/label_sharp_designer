using LabelSharpDesignerCore.Core.Document;
using LabelSharpDesignerCore.Serialization;

namespace LabelSharpDesignerCore.LegacySampleApp.Labels;

/// <summary>
/// This application's own disk-backed catalog of <c>.label</c> documents — one JSON file per document
/// under this app's own <c>%APPDATA%</c> folder (never the plugin's own
/// <c>%APPDATA%\LabelSharpDesignerCore\Labels</c>, which belongs to the plugin's <c>LibraryRepository</c>
/// and is intentionally never touched here). Listing/creating/renaming/duplicating/deleting labels is
/// entirely this app's own responsibility; the plugin (via <c>LegacyLauncher</c>) is only ever asked
/// to edit the contents of one file this repository already owns.
/// </summary>
public sealed class LabelRepository
{
    private readonly string _directory;

    private LabelRepository(string directory)
    {
        _directory = directory;
    }

    /// <summary>Opens this app's own label folder (<c>%APPDATA%\LabelSharpDesignerCore\LegacySampleApp\Labels</c>),
    /// creating it if needed.</summary>
    public static LabelRepository Open()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LabelSharpDesignerCore",
            "LegacySampleApp",
            "Labels");
        return OpenAt(directory);
    }

    /// <summary>Opens a catalog at an arbitrary directory — used by tests to avoid touching the real
    /// user profile.</summary>
    public static LabelRepository OpenAt(string directory)
    {
        Directory.CreateDirectory(directory);
        return new LabelRepository(directory);
    }

    /// <summary>Lists every readable <c>.label</c> file, most recently updated first. Files that fail
    /// to parse (corrupt or foreign format) are silently skipped rather than failing the whole
    /// listing.</summary>
    public IReadOnlyList<LabelEntry> List()
    {
        var entries = new List<LabelEntry>();
        foreach (var path in Directory.EnumerateFiles(_directory, "*.label"))
        {
            LabelDocument document;
            try
            {
                document = LabelDocumentCodec.Load(File.ReadAllText(path));
            }
            catch
            {
                continue;
            }

            entries.Add(new LabelEntry(Path.GetFileNameWithoutExtension(path), path, document));
        }

        entries.Sort((a, b) => b.Document.Metadata.UpdatedAt.CompareTo(a.Document.Metadata.UpdatedAt));
        return entries;
    }

    /// <summary>The page size/DPI a brand-new label starts with before the user picks their own.</summary>
    public static PageConfig DefaultPage => new() { WidthMm = 100, HeightMm = 60, Dpi = 203 };

    /// <summary>Creates a blank document and saves it immediately — the caller is expected to open the
    /// plugin's editor on the result right after (see <c>LabelListForm.CreateLabel</c>), mirroring
    /// INTEGRATION.md §3.3's "create a blank document before calling the editor" flow.</summary>
    public LabelEntry Create(string name, PageConfig page)
    {
        var now = DateTimeOffset.UtcNow;
        var document = new LabelDocument
        {
            Name = name,
            Page = page,
            Layers = new[] { new LabelLayer { Id = "layer-1", Name = "Base", Order = 0 } },
            Metadata = new DocumentMetadata { CreatedAt = now, UpdatedAt = now },
        };

        return Save(Guid.NewGuid().ToString("N"), document);
    }

    /// <summary>Renames a label in place — a plain metadata edit, so this app writes the file directly
    /// with <see cref="LabelDocumentCodec"/> rather than round-tripping through the plugin's editor for
    /// something that isn't a drawing change.</summary>
    public LabelEntry Rename(LabelEntry entry, string newName)
    {
        var document = entry.Document with
        {
            Name = newName,
            Metadata = entry.Document.Metadata with { UpdatedAt = DateTimeOffset.UtcNow },
        };

        return Save(entry.Id, document);
    }

    /// <summary>Saves an independent copy of <paramref name="entry"/> under a new id, named
    /// "&lt;original&gt; (cópia)" with a fresh created/updated timestamp.</summary>
    public LabelEntry Duplicate(LabelEntry entry)
    {
        var now = DateTimeOffset.UtcNow;
        var copy = entry.Document with
        {
            Name = $"{entry.Document.Name} (cópia)",
            Metadata = entry.Document.Metadata with { CreatedAt = now, UpdatedAt = now },
        };

        return Save(Guid.NewGuid().ToString("N"), copy);
    }

    public void Delete(LabelEntry entry)
    {
        if (File.Exists(entry.FilePath))
        {
            File.Delete(entry.FilePath);
        }
    }

    /// <summary>Re-reads <paramref name="entry"/>'s file from disk — used after the plugin's editor
    /// (running as the satellite process) reports it saved changes directly to
    /// <see cref="LabelEntry.FilePath"/>, since this repository's in-memory copy is now stale.</summary>
    public LabelEntry Reload(LabelEntry entry) =>
        new(entry.Id, entry.FilePath, LabelDocumentCodec.Load(File.ReadAllText(entry.FilePath)));

    private LabelEntry Save(string id, LabelDocument document)
    {
        var path = Path.Combine(_directory, $"{id}.label");
        File.WriteAllText(path, LabelDocumentCodec.Save(document));
        return new LabelEntry(id, path, document);
    }
}
