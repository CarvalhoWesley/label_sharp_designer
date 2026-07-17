using LabelSharpDesignerCore.Core.Document;
using LabelSharpDesignerCore.Core.Elements;
using LabelSharpDesignerCore.Core.Geometry;
using LabelSharpDesignerCore.UI.WinForms.Canvas;

namespace LabelSharpDesignerCore.UI.WinForms.Tests;

public class LabelCanvasControlHistoryTests
{
    private static LabelDocument SampleDocument() => new()
    {
        Name = "Test",
        Page = new PageConfig { WidthMm = 50, HeightMm = 50 },
        Elements =
        [
            new RectangleElement { Id = "r1", Position = new PointMm(0, 0), Size = new SizeMm(5, 5) },
            new RectangleElement { Id = "r2", Position = new PointMm(10, 10), Size = new SizeMm(5, 5) },
        ],
    };

    private static LabelCanvasControl NewCanvas(LabelDocument? document = null)
    {
        var canvas = new LabelCanvasControl { Document = document ?? SampleDocument() };
        return canvas;
    }

    [Fact]
    public void SelectAll_SelectsEveryTopLevelElement()
    {
        using var canvas = NewCanvas();

        canvas.SelectAll();

        Assert.Equal(["r1", "r2"], canvas.SelectedElementIds.OrderBy(id => id));
    }

    [Fact]
    public void ClearSelection_EmptiesTheSelection()
    {
        using var canvas = NewCanvas();
        canvas.SelectAll();

        canvas.ClearSelection();

        Assert.Empty(canvas.SelectedElementIds);
    }

    [Fact]
    public void DeleteSelection_RemovesElementsAndSupportsUndo()
    {
        using var canvas = NewCanvas();
        canvas.SelectAll();

        canvas.DeleteSelection();

        Assert.Empty(canvas.Document!.Elements);
        Assert.Empty(canvas.SelectedElementIds);
        Assert.True(canvas.CanUndo);

        canvas.Undo();

        Assert.Equal(2, canvas.Document!.Elements.Count);
        Assert.False(canvas.CanUndo);
        Assert.True(canvas.CanRedo);
    }

    [Fact]
    public void DeleteSelection_Redo_ReappliesTheDeletion()
    {
        using var canvas = NewCanvas();
        canvas.SelectAll();
        canvas.DeleteSelection();
        canvas.Undo();

        canvas.Redo();

        Assert.Empty(canvas.Document!.Elements);
    }

    [Fact]
    public void DuplicateSelection_AddsCopiesWithNewIdsAndSelectsThem()
    {
        using var canvas = NewCanvas();
        canvas.SelectAll();
        var originalIds = canvas.SelectedElementIds.ToHashSet();

        canvas.DuplicateSelection();

        Assert.Equal(4, canvas.Document!.Elements.Count);
        Assert.Equal(2, canvas.SelectedElementIds.Count);
        Assert.DoesNotContain(canvas.SelectedElementIds, id => originalIds.Contains(id));
        Assert.True(canvas.CanUndo);

        canvas.Undo();
        Assert.Equal(2, canvas.Document!.Elements.Count);
    }

    [Fact]
    public void DuplicateSelection_OffsetsThePositionSoCopiesAreNotStackedOnTheOriginal()
    {
        using var canvas = NewCanvas();
        canvas.SelectAll();

        canvas.DuplicateSelection();

        var originals = canvas.Document!.Elements.Where(e => e.Id is "r1" or "r2").ToList();
        var duplicates = canvas.Document.Elements.Where(e => canvas.SelectedElementIds.Contains(e.Id)).ToList();
        foreach (var duplicate in duplicates)
        {
            Assert.DoesNotContain(originals, original => original.Position == duplicate.Position);
        }
    }

    [Fact]
    public void GroupSelection_CombinesElementsIntoASingleGroup()
    {
        using var canvas = NewCanvas();
        canvas.SelectAll();

        canvas.GroupSelection();

        var only = Assert.Single(canvas.Document!.Elements);
        var group = Assert.IsType<GroupElement>(only);
        Assert.Equal(2, group.Children.Count);
        Assert.Equal([group.Id], canvas.SelectedElementIds);
    }

    [Fact]
    public void GroupSelection_ThenUngroup_RestoresTheOriginalTopLevelElements()
    {
        using var canvas = NewCanvas();
        canvas.SelectAll();
        canvas.GroupSelection();

        canvas.UngroupSelection();

        Assert.Equal(2, canvas.Document!.Elements.Count);
        Assert.All(canvas.Document.Elements, e => Assert.IsType<RectangleElement>(e));
        Assert.Equal(["r1", "r2"], canvas.SelectedElementIds.OrderBy(id => id));
    }

    [Fact]
    public void GroupSelection_WithFewerThanTwoSelected_IsANoOp()
    {
        using var canvas = NewCanvas();
        canvas.SelectAll();
        canvas.ClearSelection();

        canvas.GroupSelection();

        Assert.Equal(2, canvas.Document!.Elements.Count);
        Assert.False(canvas.CanUndo);
    }

    [Fact]
    public void MultipleOperations_UndoUnwindsEachStepInReverseOrder()
    {
        using var canvas = NewCanvas();
        canvas.SelectAll();
        canvas.DuplicateSelection(); // 2 -> 4 elements
        var afterDuplicateCount = canvas.Document!.Elements.Count;
        canvas.DeleteSelection(); // remove the 2 duplicates just selected -> back to 2

        Assert.Equal(2, canvas.Document!.Elements.Count);

        canvas.Undo(); // undo delete -> back to 4
        Assert.Equal(afterDuplicateCount, canvas.Document!.Elements.Count);

        canvas.Undo(); // undo duplicate -> back to 2 originals
        Assert.Equal(2, canvas.Document!.Elements.Count);
        Assert.False(canvas.CanUndo);
    }

    [Fact]
    public void SettingDocument_ResetsHistoryAndSelection()
    {
        using var canvas = NewCanvas();
        canvas.SelectAll();
        canvas.DeleteSelection();
        Assert.True(canvas.CanUndo);

        canvas.Document = SampleDocument();

        Assert.False(canvas.CanUndo);
        Assert.Empty(canvas.SelectedElementIds);
    }

    [Fact]
    public void AddElement_WithNewVariables_RegistersOnlyPreviouslyUndeclaredNames()
    {
        using var canvas = NewCanvas(SampleDocument() with
        {
            Variables = [new LabelVariable { Name = "existing", DefaultValue = "1" }],
        });
        var element = new VariableElement { Id = "v1", Position = new PointMm(0, 0), Size = new SizeMm(10, 5), Expression = "variavel" };

        canvas.AddElement(element, [
            new LabelVariable { Name = "existing", DefaultValue = "should not overwrite" },
            new LabelVariable { Name = "variavel", DefaultValue = "Valor" },
        ]);

        Assert.Contains(canvas.Document!.Elements, e => e.Id == "v1");
        Assert.Equal(["existing", "variavel"], canvas.Document.Variables.Select(v => v.Name).OrderBy(n => n));
        Assert.Equal("1", canvas.Document.Variables.Single(v => v.Name == "existing").DefaultValue);
    }

    [Fact]
    public void AddElement_WithNewVariables_IsUndoneInOneStep()
    {
        using var canvas = NewCanvas();
        var element = new VariableElement { Id = "v1", Position = new PointMm(0, 0), Size = new SizeMm(10, 5), Expression = "variavel" };

        canvas.AddElement(element, [new LabelVariable { Name = "variavel", DefaultValue = "Valor" }]);
        canvas.Undo();

        Assert.DoesNotContain(canvas.Document!.Elements, e => e.Id == "v1");
        Assert.Empty(canvas.Document.Variables);
    }
}
