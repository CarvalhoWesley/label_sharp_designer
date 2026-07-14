using LabelSharpDesigner.Core.Document;
using LabelSharpDesigner.Core.Elements;
using LabelSharpDesigner.Core.Geometry;
using LabelSharpDesigner.Core.Styles;

namespace LabelSharpDesigner.Serialization.Tests;

public class LabelDocumentRoundTripTests
{
    private static LabelDocument BuildSampleDocument() => new()
    {
        Name = "Sample",
        Page = new PageConfig { WidthMm = 100, HeightMm = 50, Dpi = 300, Columns = 2, ColumnGapMm = 3 },
        Layers = new[] { new LabelLayer { Id = "layer-1", Name = "Base", Order = 0 } },
        Styles = new[] { new LabelStyle { Id = "style-1", Name = "Título", Text = TextStyleSpec.Default with { Bold = true } } },
        Variables = new[] { new LabelVariable { Name = "produto.nome", Type = VariableValueType.String } },
        Elements = new LabelElement[]
        {
            new TextElement { Id = "e1", Position = new PointMm(1, 1), Size = new SizeMm(30, 5), Content = "{{produto.nome}}", StyleId = "style-1" },
            new BarcodeElement { Id = "e2", Position = new PointMm(1, 8), Size = new SizeMm(40, 10), Data = "7891234567895", Symbology = BarcodeSymbology.Ean13 },
            new QrCodeElement { Id = "e3", Position = new PointMm(60, 1), Size = new SizeMm(15, 15), Data = "https://example.com", ErrorCorrectionLevel = QrErrorCorrectionLevel.High },
            new ImageElement { Id = "e4", Position = new PointMm(1, 20), Size = new SizeMm(20, 20), Source = "logo.png", Fit = ImageFit.Cover },
            new RectangleElement { Id = "e5", Position = new PointMm(0, 0), Size = new SizeMm(100, 50), CornerRadius = 2 },
            new EllipseElement { Id = "e6", Position = new PointMm(10, 10), Size = new SizeMm(8, 8) },
            new CircleElement { Id = "e7", Position = new PointMm(20, 10), Size = new SizeMm(8, 8) },
            new LineElement { Id = "e8", Position = new PointMm(0, 25), Size = new SizeMm(100, 0), StrokeColor = ArgbColor.FromHex("#FF0000") },
            new VariableElement { Id = "e9", Position = new PointMm(1, 30), Size = new SizeMm(30, 5), Expression = "produto.preco.Currency()" },
            new DateElement { Id = "e10", Position = new PointMm(1, 36), Size = new SizeMm(30, 5), Format = "dd/MM/yyyy" },
            new TimeElement { Id = "e11", Position = new PointMm(1, 42), Size = new SizeMm(30, 5), Format = "HH:mm" },
            new TableElement
            {
                Id = "e12",
                Position = new PointMm(1, 48),
                Size = new SizeMm(90, 20),
                Columns = new[] { new TableColumn { Header = "Item", DataField = "item" }, new TableColumn { Header = "Qtd", DataField = "qty", WidthMm = 10 } },
            },
            new GroupElement
            {
                Id = "e13",
                Position = new PointMm(0, 0),
                Size = new SizeMm(10, 10),
                Children = new LabelElement[]
                {
                    new RectangleElement { Id = "e13-a", Position = new PointMm(0, 0), Size = new SizeMm(5, 5) },
                    new TextElement { Id = "e13-b", Position = new PointMm(0, 5), Size = new SizeMm(5, 5), Content = "x" },
                },
            },
        },
    };

    [Fact]
    public void SaveThenLoad_ReproducesAnEquivalentDocument()
    {
        var original = BuildSampleDocument();

        // LabelDocument's record-generated Equals does reference equality on its
        // IReadOnlyList<T> properties (List<T> vs T[] never compare equal), so we assert
        // round-trip fidelity via canonical re-serialization instead of Assert.Equal.
        var originalJson = LabelDocumentCodec.Save(original);
        var loaded = LabelDocumentCodec.Load(originalJson);
        var reserializedJson = LabelDocumentCodec.Save(loaded);

        Assert.Equal(originalJson, reserializedJson);
    }

    [Fact]
    public void Save_EmitsTypeDiscriminatorForEachElement()
    {
        var json = LabelDocumentCodec.Save(BuildSampleDocument());

        Assert.Contains("\"$type\": \"text\"", json);
        Assert.Contains("\"$type\": \"barcode\"", json);
        Assert.Contains("\"$type\": \"qrCode\"", json);
        Assert.Contains("\"$type\": \"group\"", json);
    }

    [Fact]
    public void Load_PreservesNestedGroupChildren()
    {
        var original = BuildSampleDocument();

        var loaded = LabelDocumentCodec.Load(LabelDocumentCodec.Save(original));

        var group = Assert.IsType<GroupElement>(loaded.Elements.Single(e => e.Id == "e13"));
        Assert.Equal(2, group.Children.Count);
        Assert.IsType<RectangleElement>(group.Children[0]);
        Assert.IsType<TextElement>(group.Children[1]);
    }
}
