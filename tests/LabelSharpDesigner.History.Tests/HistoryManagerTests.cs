using LabelSharpDesigner.Core.Document;
using LabelSharpDesigner.Core.Elements;
using LabelSharpDesigner.Core.Geometry;

namespace LabelSharpDesigner.History.Tests;

public class HistoryManagerTests
{
    private static LabelDocument DocumentWithRectangleAt(double x, double y) => new()
    {
        Name = "Test",
        Page = new PageConfig { WidthMm = 50, HeightMm = 50 },
        Elements = [new RectangleElement { Id = "r1", Position = new PointMm(x, y), Size = new SizeMm(5, 5) }],
    };

    [Fact]
    public void Execute_UpdatesCurrentAndEnablesUndo()
    {
        var before = DocumentWithRectangleAt(0, 0);
        var after = DocumentWithRectangleAt(10, 0);
        var history = new HistoryManager(before);

        history.Execute(new MoveCommand { Before = before, After = after, ElementIds = ["r1"] });

        Assert.Same(after, history.Current);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Undo_RestoresThePreviousDocumentAndEnablesRedo()
    {
        var before = DocumentWithRectangleAt(0, 0);
        var after = DocumentWithRectangleAt(10, 0);
        var history = new HistoryManager(before);
        history.Execute(new MoveCommand { Before = before, After = after, ElementIds = ["r1"] });

        history.Undo();

        Assert.Same(before, history.Current);
        Assert.False(history.CanUndo);
        Assert.True(history.CanRedo);
    }

    [Fact]
    public void Redo_ReappliesTheUndoneCommand()
    {
        var before = DocumentWithRectangleAt(0, 0);
        var after = DocumentWithRectangleAt(10, 0);
        var history = new HistoryManager(before);
        history.Execute(new MoveCommand { Before = before, After = after, ElementIds = ["r1"] });
        history.Undo();

        history.Redo();

        Assert.Same(after, history.Current);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Execute_AfterUndo_ClearsTheRedoStack()
    {
        var v1 = DocumentWithRectangleAt(0, 0);
        var v2 = DocumentWithRectangleAt(10, 0);
        var v3 = DocumentWithRectangleAt(20, 0);
        var history = new HistoryManager(v1);
        history.Execute(new MoveCommand { Before = v1, After = v2, ElementIds = ["r1"] });
        history.Undo();

        history.Execute(new MoveCommand { Before = v1, After = v3, ElementIds = ["r1"] });

        Assert.Same(v3, history.Current);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void PreviewChange_UpdatesCurrentButDoesNotAffectUndoStack()
    {
        var before = DocumentWithRectangleAt(0, 0);
        var midDrag = DocumentWithRectangleAt(5, 0);
        var history = new HistoryManager(before);

        history.PreviewChange(midDrag);

        Assert.Same(midDrag, history.Current);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void Undo_WithEmptyStack_IsANoOp()
    {
        var document = DocumentWithRectangleAt(0, 0);
        var history = new HistoryManager(document);

        history.Undo();

        Assert.Same(document, history.Current);
    }

    [Fact]
    public void MultipleUndoRedo_RoundTripsThroughSeveralSteps()
    {
        var v1 = DocumentWithRectangleAt(0, 0);
        var v2 = DocumentWithRectangleAt(10, 0);
        var v3 = DocumentWithRectangleAt(20, 0);
        var history = new HistoryManager(v1);
        history.Execute(new MoveCommand { Before = v1, After = v2, ElementIds = ["r1"] });
        history.Execute(new MoveCommand { Before = v2, After = v3, ElementIds = ["r1"] });

        history.Undo();
        history.Undo();
        Assert.Same(v1, history.Current);

        history.Redo();
        Assert.Same(v2, history.Current);
        history.Redo();
        Assert.Same(v3, history.Current);
    }

    [Fact]
    public void CompositeCommand_FromSpansTheFirstBeforeAndLastAfter()
    {
        var v1 = DocumentWithRectangleAt(0, 0);
        var v2 = DocumentWithRectangleAt(10, 0);
        var v3 = DocumentWithRectangleAt(20, 0);
        var commands = new ICommand[]
        {
            new MoveCommand { Before = v1, After = v2, ElementIds = ["r1"] },
            new MoveCommand { Before = v2, After = v3, ElementIds = ["r1"] },
        };

        var composite = CompositeCommand.From(commands);

        Assert.Same(v1, composite.Before);
        Assert.Same(v3, composite.After);
    }
}
