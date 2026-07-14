using System.Globalization;
using LabelSharpDesigner.Core.Document;
using LabelSharpDesigner.Core.Elements;
using LabelSharpDesigner.Core.Layout;
using LabelSharpDesigner.Expressions;
using LabelSharpDesigner.Expressions.Evaluation;

namespace LabelSharpDesigner.Layout;

/// <summary>
/// Resolves each <see cref="LabelElement"/> subtype into zero or more <see cref="ResolvedElement"/>
/// instances (mm converted to dots, {{ }} placeholders evaluated). A <see cref="GroupElement"/>
/// contributes no element of its own; it flattens into its (visible) children, composing rotation.
/// </summary>
internal sealed class ElementResolvingVisitor : IElementVisitor<IEnumerable<ResolvedElement>>
{
    private readonly int _dpi;
    private readonly LayoutOptions _options;
    private readonly EvaluationContext _evaluationContext;
    private readonly TemplateResolver _templateResolver;
    private readonly IReadOnlyDictionary<string, LabelLayer> _layersById;
    private readonly int _offsetXDots;
    private double _parentRotationDegrees;

    /// <param name="offsetXDots">Added to every resolved element's X position — used by
    /// <see cref="LayoutEngine.ResolveBatch"/> to place a record's copy of the label in its column
    /// slot within a physically wider multi-column row. Zero for a normal single-label resolve.</param>
    public ElementResolvingVisitor(int dpi, LayoutOptions options, IReadOnlyDictionary<string, LabelLayer> layersById, int offsetXDots = 0)
    {
        _dpi = dpi;
        _options = options;
        _evaluationContext = new EvaluationContext(options.SampleData);
        _templateResolver = new TemplateResolver(options.ExpressionEngine);
        _layersById = layersById;
        _offsetXDots = offsetXDots;
    }

    public IEnumerable<ResolvedElement> VisitText(TextElement element)
    {
        yield return Build(element, new ResolvedTextPayload
        {
            Text = _templateResolver.Resolve(element.Content, _evaluationContext),
            Style = element.Style,
        });
    }

    public IEnumerable<ResolvedElement> VisitBarcode(BarcodeElement element)
    {
        yield return Build(element, new ResolvedBarcodePayload
        {
            Data = _templateResolver.Resolve(element.Data, _evaluationContext),
            Symbology = element.Symbology,
            ShowText = element.ShowText,
            ModuleWidth = element.ModuleWidth,
            TextSize = element.TextSize,
        });
    }

    public IEnumerable<ResolvedElement> VisitQrCode(QrCodeElement element)
    {
        yield return Build(element, new ResolvedQrCodePayload
        {
            Data = _templateResolver.Resolve(element.Data, _evaluationContext),
            ErrorCorrectionLevel = element.ErrorCorrectionLevel,
        });
    }

    public IEnumerable<ResolvedElement> VisitImage(ImageElement element)
    {
        yield return Build(element, new ResolvedImagePayload
        {
            Source = _templateResolver.Resolve(element.Source, _evaluationContext),
            Fit = element.Fit,
        });
    }

    public IEnumerable<ResolvedElement> VisitRectangle(RectangleElement element)
    {
        yield return Build(element, new ResolvedShapePayload
        {
            Kind = ShapeKind.Rectangle,
            StrokeColor = element.Style.BorderColor,
            StrokeWidthDots = MmConversion.ToDots(element.Style.BorderWidthMm, _dpi),
            FillColor = element.Style.FillColor,
            CornerRadiusDots = MmConversion.ToDots(element.CornerRadius, _dpi),
        });
    }

    public IEnumerable<ResolvedElement> VisitEllipse(EllipseElement element)
    {
        yield return Build(element, new ResolvedShapePayload
        {
            Kind = ShapeKind.Ellipse,
            StrokeColor = element.Style.BorderColor,
            StrokeWidthDots = MmConversion.ToDots(element.Style.BorderWidthMm, _dpi),
            FillColor = element.Style.FillColor,
        });
    }

    public IEnumerable<ResolvedElement> VisitCircle(CircleElement element)
    {
        yield return Build(element, new ResolvedShapePayload
        {
            Kind = ShapeKind.Circle,
            StrokeColor = element.Style.BorderColor,
            StrokeWidthDots = MmConversion.ToDots(element.Style.BorderWidthMm, _dpi),
            FillColor = element.Style.FillColor,
        });
    }

    public IEnumerable<ResolvedElement> VisitLine(LineElement element)
    {
        yield return Build(element, new ResolvedShapePayload
        {
            Kind = ShapeKind.Line,
            StrokeColor = element.StrokeColor,
            StrokeWidthDots = MmConversion.ToDots(element.StrokeWidth, _dpi),
        });
    }

    public IEnumerable<ResolvedElement> VisitVariable(VariableElement element)
    {
        yield return Build(element, new ResolvedTextPayload
        {
            Text = _options.ExpressionEngine.EvaluateToDisplayString(element.Expression, _evaluationContext),
            Style = element.Style,
        });
    }

    public IEnumerable<ResolvedElement> VisitDate(DateElement element)
    {
        var value = ResolveDateTimeValue(element.Source, element.VariableName);
        yield return Build(element, new ResolvedTextPayload
        {
            Text = value.ToString(element.Format, CultureInfo.InvariantCulture),
            Style = element.Style,
        });
    }

    public IEnumerable<ResolvedElement> VisitTime(TimeElement element)
    {
        var value = ResolveDateTimeValue(element.Source, element.VariableName);
        yield return Build(element, new ResolvedTextPayload
        {
            Text = value.ToString(element.Format, CultureInfo.InvariantCulture),
            Style = element.Style,
        });
    }

    public IEnumerable<ResolvedElement> VisitTable(TableElement element)
    {
        yield return Build(element, new ResolvedTablePayload
        {
            Columns = element.Columns,
            RowHeightDots = MmConversion.ToDots(element.RowHeightMm, _dpi),
            HeaderStyle = element.HeaderStyle,
            CellStyle = element.CellStyle,
        });
    }

    public IEnumerable<ResolvedElement> VisitGroup(GroupElement element)
    {
        _parentRotationDegrees += element.RotationDegrees;
        try
        {
            foreach (var child in element.Children)
            {
                if (!LayoutEngine.IsVisible(child, _layersById))
                {
                    continue;
                }

                foreach (var resolved in child.Accept(this))
                {
                    yield return resolved;
                }
            }
        }
        finally
        {
            _parentRotationDegrees -= element.RotationDegrees;
        }
    }

    private ResolvedElement Build(LabelElement element, ResolvedPayload payload) => new()
    {
        SourceElementId = element.Id,
        XDots = MmConversion.ToDots(element.Position.X, _dpi) + _offsetXDots,
        YDots = MmConversion.ToDots(element.Position.Y, _dpi),
        WidthDots = MmConversion.ToDots(element.Size.Width, _dpi),
        HeightDots = MmConversion.ToDots(element.Size.Height, _dpi),
        RotationDegrees = element.RotationDegrees + _parentRotationDegrees,
        ZIndex = element.ZIndex,
        Opacity = element.Opacity,
        Transform = element.Transform,
        Payload = payload,
    };

    private DateTimeOffset ResolveDateTimeValue(DateTimeValueSource source, string? variableName)
    {
        if (source == DateTimeValueSource.Now)
        {
            return _options.Now;
        }

        if (variableName is not null && _evaluationContext.TryGetRoot(variableName, out var raw))
        {
            return raw switch
            {
                DateTimeOffset dto => dto,
                DateTime dt => dt,
                string s when DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) => parsed,
                _ => _options.Now,
            };
        }

        return _options.Now;
    }
}
