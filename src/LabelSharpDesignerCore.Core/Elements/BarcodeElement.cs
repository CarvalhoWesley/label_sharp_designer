namespace LabelSharpDesignerCore.Core.Elements;

public enum BarcodeSymbology
{
    Ean13,
    Ean8,
    Code39,
    Code128,
    Upc,
    Itf,
    Codabar,
}

public sealed record BarcodeElement : LabelElement
{
    public required string Data { get; init; }
    public required BarcodeSymbology Symbology { get; init; }
    public bool ShowText { get; init; } = true;
    public double ModuleWidth { get; init; } = 0.33;
    public double TextSize { get; init; } = 2.5;

    public override TResult Accept<TResult>(IElementVisitor<TResult> visitor) => visitor.VisitBarcode(this);
}
