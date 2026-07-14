using LabelSharpDesigner.SampleApp.Products;

namespace LabelSharpDesigner.SampleApp.Printing;

/// <summary>One row of <see cref="PrintProductsForm"/>'s grid: a product plus whether/how many labels
/// to print for it. Flattens <see cref="Product"/>'s fields onto get-only properties because
/// <see cref="DataGridView"/> data binding via <c>DataPropertyName</c> doesn't follow dotted/nested
/// paths.</summary>
public sealed class ProductPrintRow
{
    public required Product Product { get; init; }

    public string Description => Product.Description;

    public decimal Price => Product.Price;

    public string Barcode => Product.Barcode;

    public bool Selected { get; set; }

    public int Quantity { get; set; } = 1;
}
