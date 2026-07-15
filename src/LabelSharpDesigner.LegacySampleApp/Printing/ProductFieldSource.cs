namespace LabelSharpDesigner.LegacySampleApp.Printing;

/// <summary>What a single label variable should be filled with when printing a product — either one
/// of <see cref="Products.Product"/>'s own fields, or the variable's own authored default. Numeric
/// values assigned deliberately: they double as the selection index of
/// <see cref="VariableMappingForm"/>'s combo box for that field.</summary>
public enum ProductFieldSource
{
    LabelDefault = 0,
    Description = 1,
    Price = 2,
    Barcode = 3,
}
