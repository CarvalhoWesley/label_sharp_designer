using LabelSharpDesignerCore.Core.Elements;
using LabelSharpDesignerCore.Core.Geometry;

namespace LabelSharpDesignerCore.Core.Tests;

public class LabelElementImmutabilityTests
{
    [Fact]
    public void With_ProducesNewInstanceAndLeavesOriginalUnchanged()
    {
        var original = new TextElement
        {
            Id = "1",
            Position = new PointMm(0, 0),
            Size = new SizeMm(10, 10),
            Content = "before",
        };

        var moved = original with { Position = new PointMm(5, 7) };

        Assert.Equal(new PointMm(0, 0), original.Position);
        Assert.Equal(new PointMm(5, 7), moved.Position);
        Assert.NotSame(original, moved);
        Assert.Equal("before", moved.Content);
    }

    [Fact]
    public void RecordEquality_IsStructural()
    {
        var a = new RectangleElement { Id = "1", Position = new PointMm(1, 1), Size = new SizeMm(2, 2) };
        var b = new RectangleElement { Id = "1", Position = new PointMm(1, 1), Size = new SizeMm(2, 2) };

        Assert.Equal(a, b);
    }
}
