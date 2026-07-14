using System.Text.Json.Nodes;
using LabelSharpDesigner.Core.Document;

namespace LabelSharpDesigner.Serialization.Migrations;

public sealed class MigrationChain
{
    private readonly IReadOnlyDictionary<int, IMigration> _migrationsByFromVersion;

    public MigrationChain(IEnumerable<IMigration> migrations)
    {
        _migrationsByFromVersion = migrations.ToDictionary(m => m.FromVersion);
    }

    public static readonly MigrationChain Default = new(Array.Empty<IMigration>());

    public JsonObject Migrate(JsonObject document)
    {
        var version = (int?)document["version"] ?? 1;

        while (version < LabelDocument.CurrentSchemaVersion)
        {
            if (!_migrationsByFromVersion.TryGetValue(version, out var migration))
            {
                throw new InvalidOperationException(
                    $"No migration registered to upgrade a .label document from schema version {version}.");
            }

            document = migration.Migrate(document);
            document["version"] = migration.ToVersion;
            version = migration.ToVersion;
        }

        return document;
    }
}
