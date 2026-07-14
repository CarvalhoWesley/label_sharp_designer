using LabelSharpDesigner.Core.Elements;
using LabelSharpDesigner.Rendering.ArgoxPpla;

namespace LabelSharpDesigner.Rendering.ArgoxPpla.Tests;

public class PplaFieldsTests
{
    [Theory]
    [InlineData(7, 4, "0007")]
    [InlineData(0, 3, "000")]
    [InlineData(1234, 4, "1234")]
    [InlineData(-5, 4, "0000")]
    public void PplaDigits_ZeroPadsAndClampsNegativeToZero(int value, int width, string expected) =>
        Assert.Equal(expected, PplaFields.PplaDigits(value, width));

    [Theory]
    [InlineData(0, "1")]
    [InlineData(90, "4")]
    [InlineData(180, "3")]
    [InlineData(270, "2")]
    [InlineData(360, "1")]
    [InlineData(-90, "2")]
    [InlineData(45, "1")] // rounds to nearest 90°
    public void PplaOrientationCode_SnapsToNearestQuadrantAndMapsToPrinterCode(double degrees, string expected) =>
        Assert.Equal(expected, PplaFields.PplaOrientationCode(degrees));

    [Theory]
    [InlineData(0, "0")]
    [InlineData(9, "9")]
    [InlineData(10, "A")]
    [InlineData(24, "O")]
    [InlineData(30, "O")] // clamped
    [InlineData(-1, "0")] // clamped
    public void PplaScaleCode_EncodesAsDigitThenLetter(int scale, string expected) =>
        Assert.Equal(expected, PplaFields.PplaScaleCode(scale));

    [Fact]
    public void PplaAsdFontSubtype_At203Dpi_Picks12PtAsCode004()
    {
        var dots = (int)Math.Round(12 * 203 / 72.0);

        Assert.Equal("004", PplaFields.PplaAsdFontSubtype(dots, 203));
    }

    [Fact]
    public void PplaAsdFontSubtype_At300DpiOrAbove_UsesTheBareIndexNotIndexPlusOne()
    {
        var dots = (int)Math.Round(12 * 300 / 72.0);

        Assert.Equal("004", PplaFields.PplaAsdFontSubtype(dots, 300));
    }

    [Theory]
    [InlineData(203, 2)]
    [InlineData(300, 1)]
    [InlineData(400, 1)]
    [InlineData(600, 1)]
    public void PplaDotMultiplier_DoublesOnlyFor203Dpi(int dpi, int expected) =>
        Assert.Equal(expected, PplaFields.PplaDotMultiplier(dpi));

    [Theory]
    [InlineData(203, "D22")]
    [InlineData(300, "D11")]
    public void PplaDotSizeCommand_MatchesTheDotMultiplier(int dpi, string expected) =>
        Assert.Equal(expected, PplaFields.PplaDotSizeCommand(dpi));

    [Fact]
    public void PplaHundredthsOfInch_OneInchAt203Dpi_Is100()
    {
        Assert.Equal(100, PplaFields.PplaHundredthsOfInch(203, 203));
    }

    [Fact]
    public void PplaMmToHundredthsOfInch_OneInch_Is100()
    {
        Assert.Equal(100, PplaFields.PplaMmToHundredthsOfInch(25.4));
    }

    [Theory]
    [InlineData(BarcodeSymbology.Code128, true, "E")]
    [InlineData(BarcodeSymbology.Code128, false, "e")]
    [InlineData(BarcodeSymbology.Ean13, true, "F")]
    [InlineData(BarcodeSymbology.Codabar, true, "I")]
    public void PplaBarcodeTypeCode_UppercaseShowsHumanReadableText(BarcodeSymbology symbology, bool humanReadable, string expected) =>
        Assert.Equal(expected, PplaFields.PplaBarcodeTypeCode(symbology, humanReadable));

    [Fact]
    public void PplaBarcodeTypeCode_ForAnUnmappedSymbology_ReturnsNull()
    {
        Assert.Null(PplaFields.PplaBarcodeTypeCode((BarcodeSymbology)999, humanReadable: true));
    }
}
