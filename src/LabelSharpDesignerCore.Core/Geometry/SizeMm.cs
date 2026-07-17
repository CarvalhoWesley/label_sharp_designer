namespace LabelSharpDesignerCore.Core.Geometry;

public readonly record struct SizeMm(double Width, double Height)
{
    public static readonly SizeMm Zero = new(0, 0);
}
