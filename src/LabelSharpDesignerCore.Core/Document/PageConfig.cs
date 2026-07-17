namespace LabelSharpDesignerCore.Core.Document;

public enum MeasurementUnit
{
    Millimeters,
    Centimeters,
    Inches,
}

public enum PageOrientation
{
    Portrait,
    Landscape,
}

public sealed record PageMargins(double Top, double Right, double Bottom, double Left)
{
    public static readonly PageMargins Zero = new(0, 0, 0, 0);
}

public sealed record PageConfig
{
    public required double WidthMm { get; init; }
    public required double HeightMm { get; init; }
    public MeasurementUnit DisplayUnit { get; init; } = MeasurementUnit.Millimeters;
    public int Dpi { get; init; } = 203;
    public PageOrientation Orientation { get; init; } = PageOrientation.Portrait;
    public PageMargins Margins { get; init; } = PageMargins.Zero;
    public int Columns { get; init; } = 1;
    public double ColumnGapMm { get; init; }
}
