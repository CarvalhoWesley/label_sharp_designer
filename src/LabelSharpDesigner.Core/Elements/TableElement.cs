using LabelSharpDesigner.Core.Styles;

namespace LabelSharpDesigner.Core.Elements;

public sealed record TableColumn
{
    public required string Header { get; init; }
    public required string DataField { get; init; }
    public double WidthMm { get; init; } = 20;
}

public sealed record TableElement : LabelElement
{
    public required IReadOnlyList<TableColumn> Columns { get; init; }
    public double RowHeightMm { get; init; } = 5;
    public TextStyleSpec HeaderStyle { get; init; } = TextStyleSpec.Default;
    public TextStyleSpec CellStyle { get; init; } = TextStyleSpec.Default;

    public override TResult Accept<TResult>(IElementVisitor<TResult> visitor) => visitor.VisitTable(this);
}
