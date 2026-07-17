using LabelSharpDesignerCore.Core.Document;
using LabelSharpDesignerCore.Core.Elements;
using LabelSharpDesignerCore.Core.Geometry;
using LabelSharpDesignerCore.Core.Layout;
using LabelSharpDesignerCore.Core.Styles;

namespace LabelSharpDesignerCore.Layout.Tests;

public class LayoutEngineTests
{
    private readonly LayoutEngine _engine = new();

    private static LabelDocument DocumentWith(PageConfig page, params LabelElement[] elements) => new()
    {
        Name = "Test",
        Page = page,
        Elements = elements,
    };

    [Fact]
    public void Resolve_ConvertsPageAndElementMillimetersToDots()
    {
        // 25.4mm at 300dpi == exactly 300 dots.
        var page = new PageConfig { WidthMm = 25.4, HeightMm = 50.8, Dpi = 300 };
        var element = new RectangleElement { Id = "r1", Position = new PointMm(25.4, 0), Size = new SizeMm(12.7, 25.4) };

        var resolved = _engine.Resolve(DocumentWith(page, element));

        Assert.Equal(300, resolved.WidthDots);
        Assert.Equal(600, resolved.HeightDots);
        var resolvedElement = Assert.Single(resolved.Elements);
        Assert.Equal(300, resolvedElement.XDots);
        Assert.Equal(0, resolvedElement.YDots);
        Assert.Equal(150, resolvedElement.WidthDots);
        Assert.Equal(300, resolvedElement.HeightDots);
    }

    [Fact]
    public void Resolve_SkipsInvisibleElementsAndHiddenLayers()
    {
        var page = new PageConfig { WidthMm = 50, HeightMm = 50, Dpi = 203 };
        var document = new LabelDocument
        {
            Name = "Test",
            Page = page,
            Layers = new[] { new LabelLayer { Id = "hidden", Name = "Hidden", Visible = false } },
            Elements = new LabelElement[]
            {
                new RectangleElement { Id = "invisible", Position = PointMm.Zero, Size = new SizeMm(1, 1), Visible = false },
                new RectangleElement { Id = "on-hidden-layer", Position = PointMm.Zero, Size = new SizeMm(1, 1), LayerId = "hidden" },
                new RectangleElement { Id = "visible", Position = PointMm.Zero, Size = new SizeMm(1, 1) },
            },
        };

        var resolved = _engine.Resolve(document);

        var only = Assert.Single(resolved.Elements);
        Assert.Equal("visible", only.SourceElementId);
    }

    [Fact]
    public void Resolve_OrdersElementsByZIndex()
    {
        var page = new PageConfig { WidthMm = 50, HeightMm = 50, Dpi = 203 };
        var document = DocumentWith(
            page,
            new RectangleElement { Id = "front", Position = PointMm.Zero, Size = new SizeMm(1, 1), ZIndex = 5 },
            new RectangleElement { Id = "back", Position = PointMm.Zero, Size = new SizeMm(1, 1), ZIndex = 0 });

        var resolved = _engine.Resolve(document);

        Assert.Equal(new[] { "back", "front" }, resolved.Elements.Select(e => e.SourceElementId));
    }

    [Fact]
    public void Resolve_TextElement_ReplacesPlaceholdersUsingSampleData()
    {
        var page = new PageConfig { WidthMm = 50, HeightMm = 50, Dpi = 203 };
        var document = DocumentWith(
            page,
            new TextElement { Id = "t1", Position = PointMm.Zero, Size = new SizeMm(10, 5), Content = "Olá {{produto.nome}}!" });

        var options = new LayoutOptions
        {
            SampleData = new Dictionary<string, object?>
            {
                ["produto"] = new Dictionary<string, object?> { ["nome"] = "Mundo" },
            },
        };

        var resolved = _engine.Resolve(document, options);

        var payload = Assert.IsType<ResolvedTextPayload>(Assert.Single(resolved.Elements).Payload);
        Assert.Equal("Olá Mundo!", payload.Text);
    }

    /// <summary>Unlike <see cref="VariableElement.Expression"/> (a bare expression), <see cref="BarcodeElement.Data"/>
    /// goes through the same <c>TemplateResolver</c> path as <see cref="TextElement.Content"/> — free
    /// text with <c>{{ }}</c> placeholders mixed in, any variable name the label declares.</summary>
    [Fact]
    public void Resolve_BarcodeElement_ReplacesPlaceholdersUsingSampleData()
    {
        var page = new PageConfig { WidthMm = 50, HeightMm = 50, Dpi = 203 };
        var document = DocumentWith(
            page,
            new BarcodeElement { Id = "b1", Position = PointMm.Zero, Size = new SizeMm(30, 10), Data = "{{codigobarras}}", Symbology = BarcodeSymbology.Code128 });

        var options = new LayoutOptions { SampleData = new Dictionary<string, object?> { ["codigobarras"] = "7891234567890" } };

        var resolved = _engine.Resolve(document, options);

        var payload = Assert.IsType<ResolvedBarcodePayload>(Assert.Single(resolved.Elements).Payload);
        Assert.Equal("7891234567890", payload.Data);
    }

    /// <summary><see cref="QrCodeElement.Data"/> resolves the same way as <see cref="BarcodeElement.Data"/>
    /// — see <see cref="Resolve_BarcodeElement_ReplacesPlaceholdersUsingSampleData"/>.</summary>
    [Fact]
    public void Resolve_QrCodeElement_ReplacesPlaceholdersUsingSampleData()
    {
        var page = new PageConfig { WidthMm = 50, HeightMm = 50, Dpi = 203 };
        var document = DocumentWith(
            page,
            new QrCodeElement { Id = "q1", Position = PointMm.Zero, Size = new SizeMm(10, 10), Data = "{{qrcode}}" });

        var options = new LayoutOptions { SampleData = new Dictionary<string, object?> { ["qrcode"] = "https://example.com" } };

        var resolved = _engine.Resolve(document, options);

        var payload = Assert.IsType<ResolvedQrCodePayload>(Assert.Single(resolved.Elements).Payload);
        Assert.Equal("https://example.com", payload.Data);
    }

    /// <summary>Guards the contract <c>NewElementFactory</c>/<c>PropertyPanel</c> rely on:
    /// <see cref="VariableElement.Expression"/> is a bare expression evaluated directly (unlike
    /// <see cref="TextElement.Content"/>, it is never <c>{{ }}</c>-wrapped) — a regression here
    /// previously made every newly inserted variable element throw and blank the whole preview.</summary>
    [Fact]
    public void Resolve_VariableElement_EvaluatesBareExpressionAgainstSampleData()
    {
        var page = new PageConfig { WidthMm = 50, HeightMm = 50, Dpi = 203 };
        var document = DocumentWith(
            page,
            new VariableElement { Id = "v1", Position = PointMm.Zero, Size = new SizeMm(10, 5), Expression = "variavel" });

        var options = new LayoutOptions { SampleData = new Dictionary<string, object?> { ["variavel"] = "Valor" } };

        var resolved = _engine.Resolve(document, options);

        var payload = Assert.IsType<ResolvedTextPayload>(Assert.Single(resolved.Elements).Payload);
        Assert.Equal("Valor", payload.Text);
    }

    [Fact]
    public void Resolve_GroupElement_FlattensChildrenAndComposesRotation()
    {
        var page = new PageConfig { WidthMm = 50, HeightMm = 50, Dpi = 203 };
        var group = new GroupElement
        {
            Id = "g1",
            Position = PointMm.Zero,
            Size = new SizeMm(10, 10),
            RotationDegrees = 10,
            Children = new LabelElement[]
            {
                new RectangleElement { Id = "child-1", Position = PointMm.Zero, Size = new SizeMm(1, 1), RotationDegrees = 5 },
                new RectangleElement { Id = "child-2", Position = PointMm.Zero, Size = new SizeMm(1, 1), Visible = false },
            },
        };

        var resolved = _engine.Resolve(DocumentWith(page, group));

        var only = Assert.Single(resolved.Elements);
        Assert.Equal("child-1", only.SourceElementId);
        Assert.Equal(15, only.RotationDegrees);
    }

    [Fact]
    public void Resolve_DateElement_UsesProvidedNowAndFormat()
    {
        var page = new PageConfig { WidthMm = 50, HeightMm = 50, Dpi = 203 };
        var document = DocumentWith(
            page,
            new DateElement { Id = "d1", Position = PointMm.Zero, Size = new SizeMm(10, 5), Format = "dd/MM/yyyy" });

        var options = new LayoutOptions { Now = new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero) };

        var resolved = _engine.Resolve(document, options);

        var payload = Assert.IsType<ResolvedTextPayload>(Assert.Single(resolved.Elements).Payload);
        Assert.Equal("13/07/2026", payload.Text);
    }

    private static IReadOnlyDictionary<string, object?> Record(string name, object? value) =>
        new Dictionary<string, object?> { [name] = value };

    [Fact]
    public void ResolveBatch_TilesRecordsIntoPhysicalRowsOfColumnsWidth()
    {
        // 25.4mm at 100dpi == exactly 100 dots, and 2.54mm == exactly 10 dots — chosen so the math
        // below stays readable.
        var page = new PageConfig { WidthMm = 25.4, HeightMm = 50.8, Dpi = 100, Columns = 2, ColumnGapMm = 2.54 };
        var document = DocumentWith(page, new TextElement { Id = "t1", Position = PointMm.Zero, Size = new SizeMm(10, 5), Content = "{{codigo}}" });

        var records = new[] { Record("codigo", "A"), Record("codigo", "B"), Record("codigo", "C") };

        var rows = _engine.ResolveBatch(document, records);

        // 3 records at 2 columns/row == 2 physical rows (2 + 1).
        Assert.Equal(2, rows.Count);

        // Row width is always the full Columns-wide roll: 2 * 100 dots + 1 * 10 dots gap == 210 dots.
        Assert.Equal(210, rows[0].WidthDots);
        Assert.Equal(200, rows[0].HeightDots);
        Assert.Equal(210, rows[1].WidthDots);

        Assert.Equal(2, rows[0].Elements.Count);
        Assert.Single(rows[1].Elements); // the partial last row only has "C" — no blank filler element.
    }

    [Fact]
    public void ResolveBatch_OffsetsEachColumnAndSubstitutesItsOwnRecord()
    {
        var page = new PageConfig { WidthMm = 25.4, HeightMm = 50.8, Dpi = 100, Columns = 2, ColumnGapMm = 2.54 };
        var document = DocumentWith(page, new TextElement { Id = "t1", Position = PointMm.Zero, Size = new SizeMm(10, 5), Content = "{{codigo}}" });

        var records = new[] { Record("codigo", "A"), Record("codigo", "B") };

        var row = Assert.Single(_engine.ResolveBatch(document, records));

        var first = row.Elements.Single(e => ((ResolvedTextPayload)e.Payload).Text == "A");
        var second = row.Elements.Single(e => ((ResolvedTextPayload)e.Payload).Text == "B");

        Assert.Equal(0, first.XDots);
        // Second column starts one label-width + gap to the right: 100 + 10 == 110 dots.
        Assert.Equal(110, second.XDots);
    }

    [Fact]
    public void ResolveBatch_WithNoRecords_ReturnsEmpty()
    {
        var page = new PageConfig { WidthMm = 25.4, HeightMm = 50.8, Dpi = 100, Columns = 2 };
        var document = DocumentWith(page, new RectangleElement { Id = "r1", Position = PointMm.Zero, Size = new SizeMm(1, 1) });

        var rows = _engine.ResolveBatch(document, Array.Empty<IReadOnlyDictionary<string, object?>>());

        Assert.Empty(rows);
    }

    [Fact]
    public void ResolveBatch_WithColumnsOne_ProducesOneLabelPerRow()
    {
        var page = new PageConfig { WidthMm = 25.4, HeightMm = 50.8, Dpi = 100, Columns = 1 };
        var document = DocumentWith(page, new TextElement { Id = "t1", Position = PointMm.Zero, Size = new SizeMm(10, 5), Content = "{{codigo}}" });

        var records = new[] { Record("codigo", "A"), Record("codigo", "B") };

        var rows = _engine.ResolveBatch(document, records);

        Assert.Equal(2, rows.Count);
        Assert.Equal(100, rows[0].WidthDots); // exactly one label wide, no gap to account for.
        Assert.Equal("A", ((ResolvedTextPayload)Assert.Single(rows[0].Elements).Payload).Text);
        Assert.Equal("B", ((ResolvedTextPayload)Assert.Single(rows[1].Elements).Payload).Text);
    }
}
