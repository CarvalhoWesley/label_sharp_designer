namespace LabelSharpDesignerCore.SampleApp.Products;

/// <summary>Home screen for the product catalog: a grid with new/edit/delete actions. Mirrors
/// <c>LibraryForm</c>'s toolbar-plus-grid shape, just with a data grid instead of thumbnail cards
/// since products have no visual preview.</summary>
public sealed class ProductListForm : Form
{
    private readonly ProductRepository _repository;
    private readonly DataGridView _grid;

    public ProductListForm(ProductRepository repository)
    {
        _repository = repository;

        Text = "Produtos";
        Width = 720;
        Height = 520;
        StartPosition = FormStartPosition.CenterScreen;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(8, 6, 8, 6),
        };

        var newButton = new Button { Text = "+ Novo produto", AutoSize = true, FlatStyle = FlatStyle.System };
        newButton.Click += (_, _) => CreateProduct();
        var editButton = new Button { Text = "Editar", AutoSize = true, FlatStyle = FlatStyle.System };
        editButton.Click += (_, _) => EditSelectedProduct();
        var deleteButton = new Button { Text = "Excluir", AutoSize = true, FlatStyle = FlatStyle.System };
        deleteButton.Click += (_, _) => DeleteSelectedProduct();
        toolbar.Controls.Add(newButton);
        toolbar.Controls.Add(editButton);
        toolbar.Controls.Add(deleteButton);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoGenerateColumns = false,
        };
        _grid.CellDoubleClick += (_, _) => EditSelectedProduct();
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "Descrição", DataPropertyName = "Description", FillWeight = 200 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Price", HeaderText = "Preço", DataPropertyName = "Price", FillWeight = 80, DefaultCellStyle = { Format = "C2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Barcode", HeaderText = "Código de barras", DataPropertyName = "Barcode", FillWeight = 120 });

        Controls.Add(_grid);
        Controls.Add(toolbar);

        Refresh_();
    }

    private void Refresh_()
    {
        _grid.DataSource = _repository.List().ToList();
    }

    private Product? SelectedProduct() =>
        _grid.CurrentRow?.DataBoundItem as Product;

    private void CreateProduct()
    {
        using var dialog = new ProductEditForm();
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result is not { } result)
        {
            return;
        }

        _repository.Create(result.Description, result.Price, result.Barcode);
        Refresh_();
    }

    private void EditSelectedProduct()
    {
        var product = SelectedProduct();
        if (product is null)
        {
            return;
        }

        using var dialog = new ProductEditForm(product);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result is not { } result)
        {
            return;
        }

        _repository.Update(product with { Description = result.Description, Price = result.Price, Barcode = result.Barcode });
        Refresh_();
    }

    private void DeleteSelectedProduct()
    {
        var product = SelectedProduct();
        if (product is null)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            $"Excluir \"{product.Description}\" permanentemente?",
            "Excluir produto",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (result != DialogResult.Yes)
        {
            return;
        }

        _repository.Delete(product);
        Refresh_();
    }
}
