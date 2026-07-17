using LabelSharpDesignerCore.Core.Elements;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.QrCode.Internal;

namespace LabelSharpDesignerCore.Barcode;

public static class BarcodeGenerator
{
    public static BarcodeImage GenerateBarcode(string data, BarcodeSymbology symbology, int widthDots, int heightDots)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = ToBarcodeFormat(symbology),
            Options = new EncodingOptions
            {
                Width = widthDots,
                Height = heightDots,
            },
        };

        var pixelData = writer.Write(data);
        return new BarcodeImage { Width = pixelData.Width, Height = pixelData.Height, Bgra32Pixels = pixelData.Pixels };
    }

    public static BarcodeImage GenerateQrCode(string data, QrErrorCorrectionLevel errorCorrectionLevel, int sizeDots)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new QrCodeEncodingOptions
            {
                Width = sizeDots,
                Height = sizeDots,
                ErrorCorrection = ToZXingErrorCorrectionLevel(errorCorrectionLevel),
            },
        };

        var pixelData = writer.Write(data);
        return new BarcodeImage { Width = pixelData.Width, Height = pixelData.Height, Bgra32Pixels = pixelData.Pixels };
    }

    private static BarcodeFormat ToBarcodeFormat(BarcodeSymbology symbology) => symbology switch
    {
        BarcodeSymbology.Ean13 => BarcodeFormat.EAN_13,
        BarcodeSymbology.Ean8 => BarcodeFormat.EAN_8,
        BarcodeSymbology.Code39 => BarcodeFormat.CODE_39,
        BarcodeSymbology.Code128 => BarcodeFormat.CODE_128,
        BarcodeSymbology.Upc => BarcodeFormat.UPC_A,
        BarcodeSymbology.Itf => BarcodeFormat.ITF,
        BarcodeSymbology.Codabar => BarcodeFormat.CODABAR,
        _ => throw new ArgumentOutOfRangeException(nameof(symbology), symbology, "Unsupported barcode symbology."),
    };

    private static ErrorCorrectionLevel ToZXingErrorCorrectionLevel(QrErrorCorrectionLevel level) => level switch
    {
        QrErrorCorrectionLevel.Low => ErrorCorrectionLevel.L,
        QrErrorCorrectionLevel.Medium => ErrorCorrectionLevel.M,
        QrErrorCorrectionLevel.Quartile => ErrorCorrectionLevel.Q,
        QrErrorCorrectionLevel.High => ErrorCorrectionLevel.H,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unsupported QR error correction level."),
    };
}
