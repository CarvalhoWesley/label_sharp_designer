using LabelSharpDesignerCore.Core.Layout;

namespace LabelSharpDesignerCore.Rendering.Abstractions;

/// <summary>Mirrors <c>IElementVisitor&lt;TResult&gt;</c> from Core, but dispatches over resolved
/// (post-layout) payloads so every render backend (canvas, PDF, PPLA, ...) shares one dispatch shape.</summary>
public interface IResolvedPayloadVisitor<out TResult>
{
    TResult VisitText(ResolvedTextPayload payload);
    TResult VisitBarcode(ResolvedBarcodePayload payload);
    TResult VisitQrCode(ResolvedQrCodePayload payload);
    TResult VisitImage(ResolvedImagePayload payload);
    TResult VisitShape(ResolvedShapePayload payload);
    TResult VisitTable(ResolvedTablePayload payload);
}
