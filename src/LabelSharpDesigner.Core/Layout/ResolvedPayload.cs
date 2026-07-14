using LabelSharpDesigner.Core.Elements;
using LabelSharpDesigner.Core.Styles;

namespace LabelSharpDesigner.Core.Layout;

public abstract record ResolvedPayload;

public sealed record ResolvedTextPayload : ResolvedPayload
{
    public required string Text { get; init; }
    public required TextStyleSpec Style { get; init; }
}

public sealed record ResolvedBarcodePayload : ResolvedPayload
{
    public required string Data { get; init; }
    public required BarcodeSymbology Symbology { get; init; }
    public required bool ShowText { get; init; }
    public required double ModuleWidth { get; init; }
    public required double TextSize { get; init; }
}

public sealed record ResolvedQrCodePayload : ResolvedPayload
{
    public required string Data { get; init; }
    public required QrErrorCorrectionLevel ErrorCorrectionLevel { get; init; }
}

public sealed record ResolvedImagePayload : ResolvedPayload
{
    public required string Source { get; init; }
    public required ImageFit Fit { get; init; }
}

public enum ShapeKind
{
    Rectangle,
    Ellipse,
    Circle,
    Line,
}

public sealed record ResolvedShapePayload : ResolvedPayload
{
    public required ShapeKind Kind { get; init; }
    public required ArgbColor StrokeColor { get; init; }
    public required double StrokeWidthDots { get; init; }
    public ArgbColor? FillColor { get; init; }
    public double CornerRadiusDots { get; init; }
}

public sealed record ResolvedTablePayload : ResolvedPayload
{
    public required IReadOnlyList<TableColumn> Columns { get; init; }
    public required double RowHeightDots { get; init; }
    public required TextStyleSpec HeaderStyle { get; init; }
    public required TextStyleSpec CellStyle { get; init; }
}
