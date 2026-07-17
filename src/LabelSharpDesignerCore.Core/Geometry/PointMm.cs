namespace LabelSharpDesignerCore.Core.Geometry;

public readonly record struct PointMm(double X, double Y)
{
    public static readonly PointMm Zero = new(0, 0);
}
