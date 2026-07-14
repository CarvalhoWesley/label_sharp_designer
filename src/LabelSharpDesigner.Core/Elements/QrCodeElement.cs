namespace LabelSharpDesigner.Core.Elements;

public enum QrErrorCorrectionLevel
{
    Low,
    Medium,
    Quartile,
    High,
}

public sealed record QrCodeElement : LabelElement
{
    public required string Data { get; init; }
    public QrErrorCorrectionLevel ErrorCorrectionLevel { get; init; } = QrErrorCorrectionLevel.Medium;

    public override TResult Accept<TResult>(IElementVisitor<TResult> visitor) => visitor.VisitQrCode(this);
}
