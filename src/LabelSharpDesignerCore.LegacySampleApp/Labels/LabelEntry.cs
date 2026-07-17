using LabelSharpDesignerCore.Core.Document;

namespace LabelSharpDesignerCore.LegacySampleApp.Labels;

/// <summary>One entry of this application's own label catalog — deliberately a separate type from
/// the plugin's own <c>LibraryEntry</c> (in <c>LabelSharpDesignerCore.App.Library</c>, not referenced by
/// this project at all): this app owns its label metadata/storage independently of the plugin.</summary>
public sealed record LabelEntry(string Id, string FilePath, LabelDocument Document);
