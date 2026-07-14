using System.Text.Json.Nodes;
using LabelSharpDesigner.Serialization.Migrations;

namespace LabelSharpDesigner.Serialization.Tests;

public class MigrationChainTests
{
    private sealed class RenamePageDimensionsV0ToV1Migration : IMigration
    {
        public int FromVersion => 0;
        public int ToVersion => 1;

        public JsonObject Migrate(JsonObject document)
        {
            var page = document["page"]!.AsObject();
            page["widthMm"] = page["width"]!.DeepClone();
            page["heightMm"] = page["height"]!.DeepClone();
            page.Remove("width");
            page.Remove("height");
            return document;
        }
    }

    private const string LegacyV0Json = """
        {
          "version": 0,
          "name": "Legacy",
          "page": { "width": 80, "height": 40 }
        }
        """;

    [Fact]
    public void Migrate_AppliesRegisteredMigrationAndBumpsVersion()
    {
        var chain = new MigrationChain(new IMigration[] { new RenamePageDimensionsV0ToV1Migration() });
        var document = (JsonObject)JsonNode.Parse(LegacyV0Json)!;

        var migrated = chain.Migrate(document);

        Assert.Equal(1, (int)migrated["version"]!);
        Assert.Equal(80, (double)migrated["page"]!["widthMm"]!);
        Assert.Equal(40, (double)migrated["page"]!["heightMm"]!);
        Assert.Null(migrated["page"]!["width"]);
    }

    [Fact]
    public void Load_UsingLegacyDocumentAndMatchingMigration_ProducesCurrentSchemaDocument()
    {
        var chain = new MigrationChain(new IMigration[] { new RenamePageDimensionsV0ToV1Migration() });

        var document = LabelDocumentCodec.Load(LegacyV0Json, migrations: chain);

        Assert.Equal("Legacy", document.Name);
        Assert.Equal(80, document.Page.WidthMm);
        Assert.Equal(40, document.Page.HeightMm);
        Assert.Equal(Core.Document.LabelDocument.CurrentSchemaVersion, document.Version);
    }

    [Fact]
    public void Migrate_ThrowsWhenNoMigrationIsRegisteredForTheSourceVersion()
    {
        var chain = new MigrationChain(Array.Empty<IMigration>());
        var document = (JsonObject)JsonNode.Parse(LegacyV0Json)!;

        Assert.Throws<InvalidOperationException>(() => chain.Migrate(document));
    }
}
