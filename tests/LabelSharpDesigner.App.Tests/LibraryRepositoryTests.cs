using LabelSharpDesigner.App.Library;
using LabelSharpDesigner.Core.Document;

namespace LabelSharpDesigner.App.Tests;

public sealed class LibraryRepositoryTests : IDisposable
{
    private readonly string _directory;
    private readonly LibraryRepository _repository;

    public LibraryRepositoryTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "LabelSharpDesigner.Tests", Guid.NewGuid().ToString("N"));
        _repository = LibraryRepository.OpenAt(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Create_WritesAFileAndReturnsItInList()
    {
        var entry = _repository.Create();

        Assert.True(File.Exists(entry.FilePath));
        Assert.Equal("Nova etiqueta", entry.Document.Name);
        Assert.Contains(_repository.List(), e => e.Id == entry.Id);
    }

    [Fact]
    public void Create_StampsAValidPngThumbnail()
    {
        var entry = _repository.Create();

        var base64 = entry.Document.Metadata.ThumbnailPngBase64;
        Assert.False(string.IsNullOrEmpty(base64));

        var bytes = Convert.FromBase64String(base64!);
        byte[] pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        Assert.Equal(pngSignature, bytes.Take(8));
    }

    [Fact]
    public void Save_OverwritesTheSameFileAcrossRenames()
    {
        var entry = _repository.Create();
        var originalPath = entry.FilePath;

        var renamed = entry.Document with { Name = "Etiqueta renomeada" };
        var saved = _repository.Save(entry, renamed);

        Assert.Equal(entry.Id, saved.Id);
        Assert.Equal(originalPath, saved.FilePath);
        Assert.Single(Directory.EnumerateFiles(_directory, "*.label"));

        var reloaded = _repository.List().Single(e => e.Id == entry.Id);
        Assert.Equal("Etiqueta renomeada", reloaded.Document.Name);
    }

    [Fact]
    public void Duplicate_CreatesAnIndependentCopyWithANewIdAndSuffixedName()
    {
        var original = _repository.Create();

        var copy = _repository.Duplicate(original);

        Assert.NotEqual(original.Id, copy.Id);
        Assert.NotEqual(original.FilePath, copy.FilePath);
        Assert.Equal("Nova etiqueta (cópia)", copy.Document.Name);
        Assert.Equal(2, _repository.List().Count);

        // The original file on disk must be untouched by duplicating it.
        var reloadedOriginal = _repository.List().Single(e => e.Id == original.Id);
        Assert.Equal("Nova etiqueta", reloadedOriginal.Document.Name);
    }

    [Fact]
    public void Delete_RemovesTheFileFromTheList()
    {
        var entry = _repository.Create();

        _repository.Delete(entry);

        Assert.False(File.Exists(entry.FilePath));
        Assert.Empty(_repository.List());
    }

    [Fact]
    public void List_SkipsCorruptFilesInsteadOfThrowing()
    {
        var entry = _repository.Create();
        File.WriteAllText(Path.Combine(_directory, "not-json.label"), "this is not valid JSON");

        var entries = _repository.List();

        Assert.Single(entries);
        Assert.Equal(entry.Id, entries[0].Id);
    }

    [Fact]
    public void List_OrdersByUpdatedAtDescending()
    {
        var older = _repository.Create();
        Thread.Sleep(10);
        var newer = _repository.Create();

        var entries = _repository.List();

        Assert.Equal(newer.Id, entries[0].Id);
        Assert.Equal(older.Id, entries[1].Id);
    }
}
