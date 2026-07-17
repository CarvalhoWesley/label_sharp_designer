namespace LabelSharpDesignerCore.UI.WinForms.Canvas;

/// <summary>Axis-aligned bounds in document millimeters, used only for snap-math — independent of
/// <see cref="ElementGeometry"/>/SkiaSharp so it can be unit-tested with no rendering dependency.</summary>
public readonly record struct ElementBoundsMm(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
    public double CenterX => Left + Width / 2;
    public double CenterY => Top + Height / 2;
}

/// <summary>The corrected position for the element being moved, plus the guide line(s) (in mm,
/// document coordinates) to draw for whichever axes actually matched an alignment snap — null on
/// an axis with no match within threshold.</summary>
public readonly record struct AlignmentSnapResult(double AdjustedLeft, double AdjustedTop, double? GuideX, double? GuideY);

/// <summary>Grid snapping and element-to-element "smart guide" alignment snapping, ported from the
/// original Flutter <c>label_canvas</c> package's <c>snap.dart</c>/<c>alignment_snap.dart</c>.</summary>
public static class AlignmentSnap
{
    /// <summary>Rounds <paramref name="value"/> to the nearest multiple of <paramref name="gridSizeMm"/>.</summary>
    public static double SnapToGrid(double value, double gridSizeMm)
    {
        if (gridSizeMm <= 0)
        {
            return value;
        }

        return Math.Round(value / gridSizeMm) * gridSizeMm;
    }

    /// <summary>
    /// Compares <paramref name="moving"/>'s left/center/right (X) and top/center/bottom (Y) edges
    /// against the same edges of every element in <paramref name="others"/>; within
    /// <paramref name="thresholdMm"/> on a given axis, snaps that axis of <paramref name="moving"/>'s
    /// position to align exactly with the closest match.
    ///
    /// The two axes are resolved independently: an alignment match on X doesn't require one on Y,
    /// and vice versa — a caller combining this with grid snapping should fall back to the grid on
    /// whichever axis didn't get an alignment match here.
    /// </summary>
    public static AlignmentSnapResult SnapToElements(ElementBoundsMm moving, IReadOnlyList<ElementBoundsMm> others, double thresholdMm)
    {
        double? bestDx = null;
        double? snappedX = null;
        double? bestDy = null;
        double? snappedY = null;

        var movingEdgesX = new[] { moving.Left, moving.CenterX, moving.Right };
        var movingEdgesY = new[] { moving.Top, moving.CenterY, moving.Bottom };

        foreach (var other in others)
        {
            var otherEdgesX = new[] { other.Left, other.CenterX, other.Right };
            var otherEdgesY = new[] { other.Top, other.CenterY, other.Bottom };

            foreach (var movingEdge in movingEdgesX)
            {
                foreach (var otherEdge in otherEdgesX)
                {
                    var diff = otherEdge - movingEdge;
                    if (Math.Abs(diff) <= thresholdMm && (bestDx is null || Math.Abs(diff) < Math.Abs(bestDx.Value)))
                    {
                        bestDx = diff;
                        snappedX = otherEdge;
                    }
                }
            }

            foreach (var movingEdge in movingEdgesY)
            {
                foreach (var otherEdge in otherEdgesY)
                {
                    var diff = otherEdge - movingEdge;
                    if (Math.Abs(diff) <= thresholdMm && (bestDy is null || Math.Abs(diff) < Math.Abs(bestDy.Value)))
                    {
                        bestDy = diff;
                        snappedY = otherEdge;
                    }
                }
            }
        }

        return new AlignmentSnapResult(
            AdjustedLeft: moving.Left + (bestDx ?? 0),
            AdjustedTop: moving.Top + (bestDy ?? 0),
            GuideX: snappedX,
            GuideY: snappedY);
    }
}
