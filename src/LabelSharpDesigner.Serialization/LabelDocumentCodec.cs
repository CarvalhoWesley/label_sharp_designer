using System.Text.Json;
using System.Text.Json.Nodes;
using LabelSharpDesigner.Core.Document;
using LabelSharpDesigner.Serialization.Migrations;

namespace LabelSharpDesigner.Serialization;

public static class LabelDocumentCodec
{
    public static string Save(LabelDocument document, JsonSerializerOptions? options = null)
        => JsonSerializer.Serialize(document, options ?? JsonOptionsFactory.Default);

    public static LabelDocument Load(string json, MigrationChain? migrations = null, JsonSerializerOptions? options = null)
    {
        var node = JsonNode.Parse(json) as JsonObject
            ?? throw new JsonException("A .label document must be a JSON object.");

        var migrated = (migrations ?? MigrationChain.Default).Migrate(node);

        return migrated.Deserialize<LabelDocument>(options ?? JsonOptionsFactory.Default)
            ?? throw new JsonException("Failed to deserialize the .label document.");
    }
}
