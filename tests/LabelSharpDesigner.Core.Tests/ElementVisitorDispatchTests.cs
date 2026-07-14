using LabelSharpDesigner.Core.Elements;
using LabelSharpDesigner.Core.Geometry;
using LabelSharpDesigner.Core.Styles;

namespace LabelSharpDesigner.Core.Tests;

public class ElementVisitorDispatchTests
{
    private sealed class TypeNameVisitor : IElementVisitor<string>
    {
        public string VisitText(TextElement element) => nameof(TextElement);
        public string VisitBarcode(BarcodeElement element) => nameof(BarcodeElement);
        public string VisitQrCode(QrCodeElement element) => nameof(QrCodeElement);
        public string VisitImage(ImageElement element) => nameof(ImageElement);
        public string VisitRectangle(RectangleElement element) => nameof(RectangleElement);
        public string VisitEllipse(EllipseElement element) => nameof(EllipseElement);
        public string VisitCircle(CircleElement element) => nameof(CircleElement);
        public string VisitLine(LineElement element) => nameof(LineElement);
        public string VisitVariable(VariableElement element) => nameof(VariableElement);
        public string VisitDate(DateElement element) => nameof(DateElement);
        public string VisitTime(TimeElement element) => nameof(TimeElement);
        public string VisitTable(TableElement element) => nameof(TableElement);
        public string VisitGroup(GroupElement element) => nameof(GroupElement);
    }

    public static IEnumerable<object[]> AllElementKinds()
    {
        var geometry = (Position: new PointMm(1, 2), Size: new SizeMm(10, 5));

        yield return new object[] { new TextElement { Id = "1", Position = geometry.Position, Size = geometry.Size, Content = "hi" }, nameof(TextElement) };
        yield return new object[] { new BarcodeElement { Id = "2", Position = geometry.Position, Size = geometry.Size, Data = "123", Symbology = BarcodeSymbology.Code128 }, nameof(BarcodeElement) };
        yield return new object[] { new QrCodeElement { Id = "3", Position = geometry.Position, Size = geometry.Size, Data = "123" }, nameof(QrCodeElement) };
        yield return new object[] { new ImageElement { Id = "4", Position = geometry.Position, Size = geometry.Size, Source = "logo.png" }, nameof(ImageElement) };
        yield return new object[] { new RectangleElement { Id = "5", Position = geometry.Position, Size = geometry.Size }, nameof(RectangleElement) };
        yield return new object[] { new EllipseElement { Id = "6", Position = geometry.Position, Size = geometry.Size }, nameof(EllipseElement) };
        yield return new object[] { new CircleElement { Id = "7", Position = geometry.Position, Size = geometry.Size }, nameof(CircleElement) };
        yield return new object[] { new LineElement { Id = "8", Position = geometry.Position, Size = geometry.Size }, nameof(LineElement) };
        yield return new object[] { new VariableElement { Id = "9", Position = geometry.Position, Size = geometry.Size, Expression = "{{x}}" }, nameof(VariableElement) };
        yield return new object[] { new DateElement { Id = "10", Position = geometry.Position, Size = geometry.Size, Format = "dd/MM/yyyy" }, nameof(DateElement) };
        yield return new object[] { new TimeElement { Id = "11", Position = geometry.Position, Size = geometry.Size, Format = "HH:mm" }, nameof(TimeElement) };
        yield return new object[]
        {
            new TableElement
            {
                Id = "12",
                Position = geometry.Position,
                Size = geometry.Size,
                Columns = new[] { new TableColumn { Header = "H", DataField = "f" } },
            },
            nameof(TableElement),
        };
        yield return new object[] { new GroupElement { Id = "13", Position = geometry.Position, Size = geometry.Size, Children = Array.Empty<LabelElement>() }, nameof(GroupElement) };
    }

    [Theory]
    [MemberData(nameof(AllElementKinds))]
    public void Accept_DispatchesToMatchingVisitorMethod(LabelElement element, string expectedTypeName)
    {
        var result = element.Accept(new TypeNameVisitor());

        Assert.Equal(expectedTypeName, result);
    }
}
