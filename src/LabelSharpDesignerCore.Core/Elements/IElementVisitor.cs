namespace LabelSharpDesignerCore.Core.Elements;

public interface IElementVisitor<out TResult>
{
    TResult VisitText(TextElement element);
    TResult VisitBarcode(BarcodeElement element);
    TResult VisitQrCode(QrCodeElement element);
    TResult VisitImage(ImageElement element);
    TResult VisitRectangle(RectangleElement element);
    TResult VisitEllipse(EllipseElement element);
    TResult VisitCircle(CircleElement element);
    TResult VisitLine(LineElement element);
    TResult VisitVariable(VariableElement element);
    TResult VisitDate(DateElement element);
    TResult VisitTime(TimeElement element);
    TResult VisitTable(TableElement element);
    TResult VisitGroup(GroupElement element);
}
