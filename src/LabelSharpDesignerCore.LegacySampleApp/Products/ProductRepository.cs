using System.Text.Json;

namespace LabelSharpDesignerCore.LegacySampleApp.Products;

/// <summary>
/// Disk-backed catalog of <see cref="Product"/>s, a single JSON array file. Entirely owned by this
/// application — has nothing to do with the LabelSharpDesignerCore plugin.
/// </summary>
public sealed class ProductRepository
{
    private readonly string _filePath;

    private ProductRepository(string filePath)
    {
        _filePath = filePath;
    }

    public static ProductRepository Open()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LabelSharpDesignerCore",
            "LegacySampleApp");
        return OpenAt(Path.Combine(directory, "products.json"));
    }

    public static ProductRepository OpenAt(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        return new ProductRepository(filePath);
    }

    /// <summary>Failure-safe: a missing, corrupt, or unreadable catalog file just yields an empty
    /// catalog instead of blocking the product list from opening.</summary>
    public IReadOnlyList<Product> List()
    {
        try
        {
            var products = JsonSerializer.Deserialize<List<Product>>(File.ReadAllText(_filePath));
            return products is null
                ? Array.Empty<Product>()
                : products.OrderBy(product => product.Description, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        catch
        {
            return Array.Empty<Product>();
        }
    }

    public Product Create(string description, decimal price, string barcode)
    {
        var product = new Product
        {
            Id = Guid.NewGuid().ToString("N"),
            Description = description,
            Price = price,
            Barcode = barcode,
        };

        var products = List().ToList();
        products.Add(product);
        Save(products);
        return product;
    }

    public void Update(Product product)
    {
        var products = List().ToList();
        var index = products.FindIndex(p => p.Id == product.Id);
        if (index >= 0)
        {
            products[index] = product;
        }
        else
        {
            products.Add(product);
        }

        Save(products);
    }

    public void Delete(Product product)
    {
        Save(List().Where(p => p.Id != product.Id).ToList());
    }

    private void Save(IReadOnlyList<Product> products) =>
        File.WriteAllText(_filePath, JsonSerializer.Serialize(products));
}
