using LabelSharpDesignerCore.UI.WinForms.Canvas;

namespace LabelSharpDesignerCore.UI.WinForms.Tests;

public class AlignmentSnapTests
{
    [Theory]
    [InlineData(11.0, 5.0, 10.0)]
    [InlineData(12.6, 5.0, 15.0)]
    [InlineData(7.4, 5.0, 5.0)]
    [InlineData(-3.0, 5.0, -5.0)]
    public void SnapToGrid_RoundsToNearestMultiple(double value, double gridSize, double expected)
    {
        Assert.Equal(expected, AlignmentSnap.SnapToGrid(value, gridSize));
    }

    [Fact]
    public void SnapToGrid_WithNonPositiveGridSize_ReturnsValueUnchanged()
    {
        Assert.Equal(12.34, AlignmentSnap.SnapToGrid(12.34, 0));
        Assert.Equal(12.34, AlignmentSnap.SnapToGrid(12.34, -5));
    }

    [Fact]
    public void SnapToElements_WithNoOtherElements_ReturnsOriginalPositionAndNoGuides()
    {
        var moving = new ElementBoundsMm(10, 10, 5, 5);

        var result = AlignmentSnap.SnapToElements(moving, [], thresholdMm: 1.0);

        Assert.Equal(10, result.AdjustedLeft);
        Assert.Equal(10, result.AdjustedTop);
        Assert.Null(result.GuideX);
        Assert.Null(result.GuideY);
    }

    [Fact]
    public void SnapToElements_LeftEdgeWithinThreshold_SnapsToOtherLeftEdgeAndReturnsGuide()
    {
        var moving = new ElementBoundsMm(10.6, 30, 5, 5);
        var other = new ElementBoundsMm(10, 0, 5, 5);

        var result = AlignmentSnap.SnapToElements(moving, [other], thresholdMm: 1.0);

        Assert.Equal(10, result.AdjustedLeft);
        Assert.Equal(10, result.GuideX);
    }

    [Fact]
    public void SnapToElements_CenterAlignsWithOtherCenter()
    {
        // moving center = 12.4 + 5/2 = 14.9, other center = 20 + 10/2 = 25 -> not aligned on this case;
        // construct so the centers are within threshold instead.
        var moving = new ElementBoundsMm(19.6, 30, 5, 5); // center X = 22.1
        var other = new ElementBoundsMm(17, 0, 10, 5); // center X = 22

        var result = AlignmentSnap.SnapToElements(moving, [other], thresholdMm: 1.0);

        // moving center should land exactly on other's center (22): adjusted left = 22 - width/2 = 19.5
        Assert.Equal(19.5, result.AdjustedLeft, precision: 6);
        Assert.Equal(22, result.GuideX);
    }

    [Fact]
    public void SnapToElements_BeyondThreshold_DoesNotSnap()
    {
        var moving = new ElementBoundsMm(20, 30, 5, 5);
        var other = new ElementBoundsMm(10, 0, 5, 5);

        var result = AlignmentSnap.SnapToElements(moving, [other], thresholdMm: 1.0);

        Assert.Equal(20, result.AdjustedLeft);
        Assert.Null(result.GuideX);
    }

    [Fact]
    public void SnapToElements_XAndYAxesResolveIndependently()
    {
        var moving = new ElementBoundsMm(10.4, 50, 5, 5);
        var xMatch = new ElementBoundsMm(10, 0, 5, 5);
        var yMatch = new ElementBoundsMm(80, 50.3, 5, 5);

        var result = AlignmentSnap.SnapToElements(moving, [xMatch, yMatch], thresholdMm: 1.0);

        Assert.Equal(10, result.AdjustedLeft);
        Assert.Equal(50.3, result.AdjustedTop);
        Assert.Equal(10, result.GuideX);
        Assert.Equal(50.3, result.GuideY);
    }

    [Fact]
    public void SnapToElements_PicksClosestMatchAmongMultipleCandidates()
    {
        var moving = new ElementBoundsMm(10.8, 30, 5, 5);
        var farther = new ElementBoundsMm(10, 0, 5, 5); // diff 0.8
        var closer = new ElementBoundsMm(10.9, 0, 5, 5); // diff 0.1

        var result = AlignmentSnap.SnapToElements(moving, [farther, closer], thresholdMm: 1.0);

        Assert.Equal(10.9, result.AdjustedLeft, precision: 6);
    }
}
