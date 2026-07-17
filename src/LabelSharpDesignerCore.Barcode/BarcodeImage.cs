namespace LabelSharpDesignerCore.Barcode;

/// <summary>Raw BGRA32 pixel buffer, matching ZXing's <c>PixelData</c> layout so renderers can wrap it directly.</summary>
public sealed record BarcodeImage
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required byte[] Bgra32Pixels { get; init; }
}
