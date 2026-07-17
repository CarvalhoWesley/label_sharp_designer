using LabelSharpDesignerCore.Core.Document;
using LabelSharpDesignerCore.Core.Elements;
using LabelSharpDesignerCore.Core.Geometry;
using LabelSharpDesignerCore.UI.WinForms.Canvas;

namespace LabelSharpDesignerCore.UI.WinForms.Tests;

public class LabelCanvasControlPropertyAndLayerTests
{
    private static LabelDocument SampleDocument() => new()
    {
        Name = "Test",
        Page = new PageConfig { WidthMm = 50, HeightMm = 50 },
        Layers =
        [
            new LabelLayer { Id = "layer-1", Name = "Base", Order = 0 },
            new LabelLayer { Id = "layer-2", Name = "Locked", Order = 1, Locked = true },
            new LabelLayer { Id = "layer-3", Name = "Hidden", Order = 2, Visible = false },
        ],
        Elements =
        [
            new RectangleElement { Id = "r1", Position = new PointMm(0, 0), Size = new SizeMm(5, 5), LayerId = "layer-1" },
            new RectangleElement { Id = "r2", Position = new PointMm(10, 10), Size = new SizeMm(5, 5), LayerId = "layer-2" },
            new RectangleElement { Id = "r3", Position = new PointMm(20, 20), Size = new SizeMm(5, 5), LayerId = "layer-3" },
            new RectangleElement { Id = "r4", Position = new PointMm(30, 30), Size = new SizeMm(5, 5), Locked = true },
        ],
    };

    private static LabelCanvasControl NewCanvas(LabelDocument? document = null) => new() { Document = document ?? SampleDocument() };

    [Fact]
    public void SelectAll_SkipsElementsOnLockedOrHiddenLayersAndDirectlyLockedElements()
    {
        using var canvas = NewCanvas();

        canvas.SelectAll();

        Assert.Equal(["r1"], canvas.SelectedElementIds);
    }

    [Fact]
    public void ApplyPropertyChange_UpdatesSelectedElementAndSupportsUndo()
    {
        using var canvas = NewCanvas();
        canvas.SelectAll(); // selects only r1

        canvas.ApplyPropertyChange(e => e with { Name = "Renamed" }, "Nome");

        var updated = canvas.Document!.Elements.Single(e => e.Id == "r1");
        Assert.Equal("Renamed", updated.Name);
        Assert.True(canvas.CanUndo);

        canvas.Undo();
        Assert.Null(canvas.Document!.Elements.Single(e => e.Id == "r1").Name);
    }

    [Fact]
    public void ApplyPropertyChange_WithNoSelection_IsANoOp()
    {
        using var canvas = NewCanvas();

        canvas.ApplyPropertyChange(e => e with { Name = "Should not apply" }, "Nome");

        Assert.False(canvas.CanUndo);
    }

    [Fact]
    public void ApplyPropertyChange_WhenApplyReturnsAnEquivalentElement_DoesNotPushAnUndoStep()
    {
        using var canvas = NewCanvas();
        canvas.SelectAll();

        canvas.ApplyPropertyChange(e => e with { }, "No-op");

        Assert.False(canvas.CanUndo);
    }

    [Fact]
    public void ApplyPropertyChange_BatchesMultipleSelectedElementsIntoOneUndoStep()
    {
        var document = new LabelDocument
        {
            Name = "Multi",
            Page = new PageConfig { WidthMm = 50, HeightMm = 50 },
            Elements =
            [
                new RectangleElement { Id = "a", Position = new PointMm(0, 0), Size = new SizeMm(5, 5) },
                new RectangleElement { Id = "b", Position = new PointMm(10, 10), Size = new SizeMm(5, 5) },
            ],
        };
        using var multiCanvas = NewCanvas(document);
        multiCanvas.SelectAll();

        multiCanvas.ApplyPropertyChange(e => e with { Visible = false }, "Visibilidade");

        Assert.All(multiCanvas.Document!.Elements, e => Assert.False(e.Visible));
        Assert.True(multiCanvas.CanUndo);

        multiCanvas.Undo();
        Assert.All(multiCanvas.Document!.Elements, e => Assert.True(e.Visible));
    }

    [Fact]
    public void ChangeDocument_AppliesAWholeDocumentEditAsOneUndoStep()
    {
        using var canvas = NewCanvas();

        canvas.ChangeDocument(doc => doc with { Layers = doc.Layers.Append(new LabelLayer { Id = "layer-4", Name = "New" }).ToList() }, "Adicionar camada");

        Assert.Equal(4, canvas.Document!.Layers.Count);
        Assert.True(canvas.CanUndo);

        canvas.Undo();
        Assert.Equal(3, canvas.Document!.Layers.Count);
    }

    [Fact]
    public void ChangeDocument_WhenApplyReturnsTheSameDocument_IsANoOp()
    {
        using var canvas = NewCanvas();

        canvas.ChangeDocument(doc => doc, "No-op");

        Assert.False(canvas.CanUndo);
    }

    [Fact]
    public void GridAndSnapSettings_LiveOnTheDocumentAndSwapWithIt()
    {
        using var canvas = NewCanvas();
        canvas.GridSizeMm = 2.5;
        canvas.ShowGrid = false;

        // They round-trip through Document itself (so saving the label preserves them) ...
        Assert.Equal(2.5, canvas.Document!.EditorSettings.GridSizeMm);
        Assert.False(canvas.Document!.EditorSettings.ShowGrid);

        // ... and a different document brings its own settings with it rather than inheriting
        // whatever the previous document happened to be showing.
        canvas.Document = SampleDocument();

        Assert.Equal(EditorSettings.Default.GridSizeMm, canvas.GridSizeMm);
        Assert.True(canvas.ShowGrid);
    }

    [Fact]
    public void GridSettingChange_DoesNotCreateAnUndoStep()
    {
        using var canvas = NewCanvas();

        canvas.GridSizeMm = 5;

        Assert.False(canvas.CanUndo);
    }
}
