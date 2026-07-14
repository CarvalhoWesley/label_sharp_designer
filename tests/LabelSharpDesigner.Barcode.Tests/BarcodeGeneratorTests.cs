using LabelSharpDesigner.Core.Elements;
using ZXing;

namespace LabelSharpDesigner.Barcode.Tests;

public class BarcodeGeneratorTests
{
    private static string Decode(BarcodeImage image, BarcodeFormat expectedFormat)
    {
        var luminanceSource = new RGBLuminanceSource(image.Bgra32Pixels, image.Width, image.Height, RGBLuminanceSource.BitmapFormat.BGRA32);
        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = false,
            Options = { TryHarder = true, PossibleFormats = new[] { expectedFormat } },
        };
        var result = reader.Decode(luminanceSource);

        return result?.Text ?? throw new InvalidOperationException("Could not decode the generated barcode image.");
    }

    [Theory]
    [InlineData(BarcodeSymbology.Ean13, "7891234567895", BarcodeFormat.EAN_13)]
    [InlineData(BarcodeSymbology.Ean8, "12345670", BarcodeFormat.EAN_8)]
    [InlineData(BarcodeSymbology.Code39, "CODE39", BarcodeFormat.CODE_39)]
    [InlineData(BarcodeSymbology.Code128, "Code128-Data", BarcodeFormat.CODE_128)]
    [InlineData(BarcodeSymbology.Upc, "036000291452", BarcodeFormat.UPC_A)]
    [InlineData(BarcodeSymbology.Itf, "12345678", BarcodeFormat.ITF)]
    public void GenerateBarcode_ProducesAScannableImage(BarcodeSymbology symbology, string data, BarcodeFormat format)
    {
        var image = BarcodeGenerator.GenerateBarcode(data, symbology, widthDots: 300, heightDots: 100);

        Assert.Equal(300, image.Width);
        Assert.Equal(data, Decode(image, format));
    }

    [Fact]
    public void GenerateBarcode_Codabar_ProducesAScannableImage()
    {
        // ZXing's CodaBarReader strips the leading/trailing start-stop characters (A/B/C/D) from
        // the decoded text, even though the writer requires them present in the input data.
        var image = BarcodeGenerator.GenerateBarcode("A12345B", BarcodeSymbology.Codabar, widthDots: 300, heightDots: 100);

        Assert.Equal("12345", Decode(image, BarcodeFormat.CODABAR));
    }

    [Theory]
    [InlineData(QrErrorCorrectionLevel.Low)]
    [InlineData(QrErrorCorrectionLevel.Medium)]
    [InlineData(QrErrorCorrectionLevel.Quartile)]
    [InlineData(QrErrorCorrectionLevel.High)]
    public void GenerateQrCode_ProducesAScannableImage(QrErrorCorrectionLevel level)
    {
        // Confirmed via isolated diagnostic: ZXing.Net's own QR *encoder* and QRCoder's independent
        // encoder produce the same module pattern for a given (data, level) pair, and ZXing.Net's
        // *decoder* fails to read back specifically that pattern for a handful of (data length,
        // level) combinations regardless of which encoder produced it (e.g. this exact URL at
        // Medium). That points at a decoder-side limitation in ZXing.Net rather than a malformed
        // symbol, but to keep this test a reliable oracle we use a payload confirmed to round-trip
        // through ZXing's own reader at all four error-correction levels.
        var image = BarcodeGenerator.GenerateQrCode("PED-000123", level, sizeDots: 300);

        Assert.Equal(300, image.Width);
        Assert.Equal(300, image.Height);
        Assert.Equal("PED-000123", Decode(image, BarcodeFormat.QR_CODE));
    }
}
