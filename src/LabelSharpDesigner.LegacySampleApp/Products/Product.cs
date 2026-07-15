namespace LabelSharpDesigner.LegacySampleApp.Products;

public sealed record Product
{
    public required string Id { get; init; }
    public required string Description { get; init; }
    public required decimal Price { get; init; }
    public required string Barcode { get; init; }
}
