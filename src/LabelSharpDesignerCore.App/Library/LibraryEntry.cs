using LabelSharpDesignerCore.Core.Document;

namespace LabelSharpDesignerCore.App.Library;

/// <summary>Pairs a decoded <see cref="LabelDocument"/> with the id/path of the <c>.label</c> file it
/// was read from. The id is the filename (a GUID) — never the document's display <c>Name</c>, so
/// renaming a document never renames or moves its file.</summary>
public sealed record LibraryEntry(string Id, string FilePath, LabelDocument Document);
