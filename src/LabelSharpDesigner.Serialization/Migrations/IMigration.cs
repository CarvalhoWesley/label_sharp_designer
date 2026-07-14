using System.Text.Json.Nodes;

namespace LabelSharpDesigner.Serialization.Migrations;

/// <summary>
/// Upgrades a raw .label JSON document by exactly one schema version. Operates on the
/// loosely-typed <see cref="JsonObject"/> representation because the source shape may no
/// longer match the current <c>LabelDocument</c> record.
/// </summary>
public interface IMigration
{
    int FromVersion { get; }

    int ToVersion { get; }

    JsonObject Migrate(JsonObject document);
}
