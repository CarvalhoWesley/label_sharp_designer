using LabelSharpDesigner.LegacySampleApp.Products;

namespace LabelSharpDesigner.LegacySampleApp.Printing;

/// <summary>One row of <see cref="PrintProductsForm"/>'s grid: a product plus whether/how many labels
/// to print for it.</summary>
public sealed class ProductPrintRow
{
    public required Product Product { get; init; }

    public string Description => Product.Description;

    public decimal Price => Product.Price;

    public string Barcode => Product.Barcode;

    public bool Selected { get; set; }

    public int Quantity { get; set; } = 1;
}
