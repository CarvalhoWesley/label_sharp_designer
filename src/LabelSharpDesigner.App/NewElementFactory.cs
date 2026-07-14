using LabelSharpDesigner.Core.Elements;
using LabelSharpDesigner.Core.Geometry;
using LabelSharpDesigner.Core.Styles;

namespace LabelSharpDesigner.App;

internal enum NewElementKind
{
    Text,
    Rectangle,
    Ellipse,
    Circle,
    Line,
    Barcode,
    QrCode,
    Date,
    Time,
    Image,
    Table,
}

/// <summary>Builds a ready-to-place default instance of each <see cref="LabelElement"/> subtype for
/// the editor's "Adicionar" toolbar menu — reasonable size/content so it's immediately visible and
/// valid (e.g. a barcode symbology that accepts arbitrary data), never so it needs no further editing.
///
/// <para>Deliberately has no case for <see cref="VariableElement"/>: it evaluates a bare expression
/// with no surrounding text, which is exactly what a <see cref="TextElement"/> whose entire
/// <c>Content</c> is a single <c>{{ expression }}</c> already does — <c>TemplateResolver</c> resolves
/// that placeholder and returns just its value, nothing else. There is no longer any label a
/// <c>VariableElement</c> could express that a <c>TextElement</c> can't, so new labels only ever get
/// offered the strictly more capable, less error-prone option (no separate "bare expression, not
/// <c>{{ }}</c>" rule to remember — see <c>ARCHITECTURE.md</c> §8). <c>VariableElement</c> itself is
/// unchanged everywhere else (model, layout, rendering, serialization, property panel) purely for
/// backward compatibility: a <c>.label</c> file saved before this change still opens, edits, renders,
/// and prints exactly as before.</para></summary>
internal static class NewElementFactory
{
    public static string Label(NewElementKind kind) => kind switch
    {
        NewElementKind.Text => "Texto",
        NewElementKind.Rectangle => "Retângulo",
        NewElementKind.Ellipse => "Elipse",
        NewElementKind.Circle => "Círculo",
        NewElementKind.Line => "Linha",
        NewElementKind.Barcode => "Código de barras",
        NewElementKind.QrCode => "QR Code",
        NewElementKind.Date => "Data",
        NewElementKind.Time => "Hora",
        NewElementKind.Image => "Imagem",
        NewElementKind.Table => "Tabela",
        _ => kind.ToString(),
    };

    /// <summary>Creates a new element of <paramref name="kind"/> centered on <paramref name="centerMm"/>,
    /// on top of everything else (<paramref name="zIndex"/>) and in <paramref name="layerId"/>.</summary>
    public static LabelElement Create(NewElementKind kind, PointMm centerMm, int zIndex, string? layerId)
    {
        var size = DefaultSize(kind);
        var position = new PointMm(centerMm.X - size.Width / 2, centerMm.Y - size.Height / 2);
        var id = Guid.NewGuid().ToString("N");

        LabelElement element = kind switch
        {
            NewElementKind.Text => new TextElement
            {
                Id = id,
                Position = position,
                Size = size,
                Content = "Texto",
                Style = TextStyleSpec.Default,
            },
            NewElementKind.Rectangle => new RectangleElement
            {
                Id = id,
                Position = position,
                Size = size,
                Style = ShapeStyleSpec.Default,
            },
            NewElementKind.Ellipse => new EllipseElement
            {
                Id = id,
                Position = position,
                Size = size,
                Style = ShapeStyleSpec.Default,
            },
            NewElementKind.Circle => new CircleElement
            {
                Id = id,
                Position = position,
                Size = size,
                Style = ShapeStyleSpec.Default,
            },
            NewElementKind.Line => new LineElement
            {
                Id = id,
                Position = position,
                Size = size,
                StrokeColor = ArgbColor.Black,
                StrokeWidth = 0.3,
            },
            NewElementKind.Barcode => new BarcodeElement
            {
                Id = id,
                Position = position,
                Size = size,
                Data = "123456789012",
                Symbology = BarcodeSymbology.Code128,
            },
            NewElementKind.QrCode => new QrCodeElement
            {
                Id = id,
                Position = position,
                Size = size,
                Data = "https://",
            },
            NewElementKind.Date => new DateElement
            {
                Id = id,
                Position = position,
                Size = size,
                Format = "dd/MM/yyyy",
                Style = TextStyleSpec.Default,
            },
            NewElementKind.Time => new TimeElement
            {
                Id = id,
                Position = position,
                Size = size,
                Format = "HH:mm",
                Style = TextStyleSpec.Default,
            },
            NewElementKind.Image => new ImageElement
            {
                Id = id,
                Position = position,
                Size = size,
                Source = string.Empty,
            },
            NewElementKind.Table => new TableElement
            {
                Id = id,
                Position = position,
                Size = size,
                Columns = [new TableColumn { Header = "Coluna 1", DataField = "campo1" }],
            },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        return element with { Name = Label(kind), LayerId = layerId, ZIndex = zIndex };
    }

    private static SizeMm DefaultSize(NewElementKind kind) => kind switch
    {
        NewElementKind.Line => new SizeMm(40, 0),
        NewElementKind.Circle => new SizeMm(20, 20),
        NewElementKind.QrCode => new SizeMm(25, 25),
        NewElementKind.Text => new SizeMm(30, 8),
        NewElementKind.Date or NewElementKind.Time => new SizeMm(30, 8),
        NewElementKind.Barcode => new SizeMm(50, 20),
        NewElementKind.Image => new SizeMm(30, 30),
        NewElementKind.Table => new SizeMm(60, 30),
        _ => new SizeMm(30, 20),
    };
}
