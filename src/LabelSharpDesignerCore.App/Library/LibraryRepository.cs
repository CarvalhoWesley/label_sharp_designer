using LabelSharpDesignerCore.Core.Document;
using LabelSharpDesignerCore.Layout;
using LabelSharpDesignerCore.Serialization;

namespace LabelSharpDesignerCore.App.Library;

/// <summary>
/// Disk-backed library of <c>.label</c> documents, one JSON file per document named
/// <c>&lt;id&gt;.label</c> where <c>id</c> is a GUID generated at creation time. There is no separate
/// index/manifest — the directory listing is the source of truth, so it can never drift out of sync
/// with what's actually on disk. Mirrors the original Flutter <c>library_repository.dart</c>.
/// </summary>
public sealed class LibraryRepository
{
    private readonly string _directory;
    private readonly LayoutEngine _layoutEngine = new();

    private LibraryRepository(string directory)
    {
        _directory = directory;
    }

    /// <summary>Opens the default library directory (<c>%APPDATA%\LabelSharpDesignerCore\Labels</c>),
    /// creating it if needed.</summary>
    public static LibraryRepository Open()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LabelSharpDesignerCore",
            "Labels");
        return OpenAt(directory);
    }

    /// <summary>Opens a library at an arbitrary directory — used by tests to avoid touching the real
    /// user profile.</summary>
    public static LibraryRepository OpenAt(string directory)
    {
        Directory.CreateDirectory(directory);
        return new LibraryRepository(directory);
    }

    /// <summary>Lists every readable <c>.label</c> file, most recently updated first. Files that fail
    /// to parse (corrupt or foreign format) are silently skipped rather than failing the whole
    /// listing.</summary>
    public IReadOnlyList<LibraryEntry> List()
    {
        var entries = new List<LibraryEntry>();
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

            entries.Add(new LibraryEntry(Path.GetFileNameWithoutExtension(path), path, document));
        }

        entries.Sort((a, b) => b.Document.Metadata.UpdatedAt.CompareTo(a.Document.Metadata.UpdatedAt));
        return entries;
    }

    /// <summary>The page size/DPI/margins a brand-new document starts with before the user has had a
    /// chance to pick their own in the "new label" page-settings dialog.</summary>
    public static PageConfig DefaultPage => new() { WidthMm = 100, HeightMm = 60, Dpi = 203 };

    /// <summary>Creates a blank document, saves it immediately (with a thumbnail), and returns the new
    /// entry.</summary>
    public LibraryEntry Create(PageConfig? page = null)
    {
        var document = new LabelDocument
        {
            Name = "Nova etiqueta",
            Page = page ?? DefaultPage,
            Layers = [new LabelLayer { Id = "layer-1", Name = "Base", Order = 0 }],
        };

        return SaveNew(Guid.NewGuid().ToString("N"), document);
    }

    /// <summary>Overwrites the file backing <paramref name="entry"/> with <paramref name="document"/>,
    /// regardless of whether the document's <c>Name</c> changed — the file identity is the entry's id,
    /// never the display name.</summary>
    public LibraryEntry Save(LibraryEntry entry, LabelDocument document) => SaveNew(entry.Id, document);

    /// <summary>Saves an independent copy of <paramref name="entry"/> under a new id, named
    /// "&lt;original&gt; (cópia)" with a fresh created/updated timestamp.</summary>
    public LibraryEntry Duplicate(LibraryEntry entry)
    {
        var now = DateTimeOffset.UtcNow;
        var copy = entry.Document with
        {
            Name = $"{entry.Document.Name} (cópia)",
            Metadata = entry.Document.Metadata with { CreatedAt = now, UpdatedAt = now },
        };

        return SaveNew(Guid.NewGuid().ToString("N"), copy);
    }

    public void Delete(LibraryEntry entry)
    {
        if (File.Exists(entry.FilePath))
        {
            File.Delete(entry.FilePath);
        }
    }

    private LibraryEntry SaveNew(string id, LabelDocument document)
    {
        var stamped = StampThumbnail(document);
        var path = Path.Combine(_directory, $"{id}.label");
        File.WriteAllText(path, LabelDocumentCodec.Save(stamped));
        return new LibraryEntry(id, path, stamped);
    }

    /// <summary>Resolves the document against each variable's default value and renders a ~240px-wide
    /// thumbnail into <see cref="DocumentMetadata.ThumbnailPngBase64"/>. Failure-safe: if resolution or
    /// rendering throws (e.g. a mid-edit invalid element), the save proceeds with no thumbnail instead
    /// of failing outright.</summary>
    private LabelDocument StampThumbnail(LabelDocument document)
    {
        var now = DateTimeOffset.UtcNow;
        try
        {
            var sampleData = document.Variables.ToDictionary(
                variable => variable.Name,
                object? (variable) => variable.DefaultValue,
                StringComparer.Ordinal);
            var resolved = _layoutEngine.Resolve(document, new LayoutOptions { SampleData = sampleData });
            var png = Rendering.Png.PngExporter.ExportScaled(resolved, targetWidthPx: 240);

            return document with
            {
                Metadata = document.Metadata with
                {
                    ThumbnailPngBase64 = Convert.ToBase64String(png),
                    UpdatedAt = now,
                },
            };
        }
        catch
        {
            return document with { Metadata = document.Metadata with { UpdatedAt = now } };
        }
    }
}
