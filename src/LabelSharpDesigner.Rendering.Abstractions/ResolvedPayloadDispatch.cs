using LabelSharpDesigner.Core.Layout;

namespace LabelSharpDesigner.Rendering.Abstractions;

public static class ResolvedPayloadDispatch
{
    public static TResult Accept<TResult>(this ResolvedPayload payload, IResolvedPayloadVisitor<TResult> visitor) => payload switch
    {
        ResolvedTextPayload p => visitor.VisitText(p),
        ResolvedBarcodePayload p => visitor.VisitBarcode(p),
        ResolvedQrCodePayload p => visitor.VisitQrCode(p),
        ResolvedImagePayload p => visitor.VisitImage(p),
        ResolvedShapePayload p => visitor.VisitShape(p),
        ResolvedTablePayload p => visitor.VisitTable(p),
        _ => throw new NotSupportedException($"Unsupported resolved payload '{payload.GetType().Name}'."),
    };
}
