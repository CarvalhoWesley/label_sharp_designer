namespace LabelSharpDesignerCore.Layout;

public static class MmConversion
{
    private const double MillimetersPerInch = 25.4;

    public static int ToDots(double millimeters, int dpi)
        => (int)Math.Round(millimeters / MillimetersPerInch * dpi, MidpointRounding.AwayFromZero);
}
