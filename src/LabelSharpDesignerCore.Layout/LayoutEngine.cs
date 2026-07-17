using LabelSharpDesignerCore.Core.Document;
using LabelSharpDesignerCore.Core.Elements;
using LabelSharpDesignerCore.Core.Layout;

namespace LabelSharpDesignerCore.Layout;

public sealed class LayoutEngine
{
    public ResolvedDocument Resolve(LabelDocument document, LayoutOptions? options = null)
    {
        options ??= new LayoutOptions();
        var dpi = document.Page.Dpi;
        var layersById = document.Layers.ToDictionary(layer => layer.Id);
        var visitor = new ElementResolvingVisitor(dpi, options, layersById);

        var resolvedElements = document.Elements
            .Where(element => IsVisible(element, layersById))
            .SelectMany(element => element.Accept(visitor))
            .OrderBy(resolved => resolved.ZIndex)
            .ToList();

        return new ResolvedDocument
        {
            WidthDots = MmConversion.ToDots(document.Page.WidthMm, dpi),
            HeightDots = MmConversion.ToDots(document.Page.HeightMm, dpi),
            Dpi = dpi,
            Elements = resolvedElements,
        };
    }

    /// <summary>Mail-merge/multi-lane resolve: tiles <paramref name="records"/> — one set of sample
    /// data per physical label — into physical rows of <see cref="PageConfig.Columns"/> labels each,
    /// side by side with <see cref="PageConfig.ColumnGapMm"/> between them. Each returned
    /// <see cref="ResolvedDocument"/> is one physical row/page — always exactly
    /// <see cref="PageConfig.Columns"/> labels wide (even a final partial row, since a physical
    /// multi-lane roll doesn't get narrower just because fewer records were supplied), with fewer
    /// populated column slots on the last row if <paramref name="records"/> doesn't divide evenly.
    /// With <c>Columns == 1</c> this degenerates to one label per row, still useful on its own as a
    /// non-tiled multi-record "print each record on its own page" pass. An empty <paramref name="records"/>
    /// returns an empty list — the caller decides how to handle "nothing to print".</summary>
    public IReadOnlyList<ResolvedDocument> ResolveBatch(LabelDocument document, IReadOnlyList<IReadOnlyDictionary<string, object?>> records, LayoutOptions? options = null)
    {
        options ??= new LayoutOptions();
        var dpi = document.Page.Dpi;
        var columns = Math.Max(document.Page.Columns, 1);
        var layersById = document.Layers.ToDictionary(layer => layer.Id);

        var labelWidthMm = document.Page.WidthMm;
        var columnGapMm = document.Page.ColumnGapMm;
        var rowWidthDots = MmConversion.ToDots(columns * labelWidthMm + (columns - 1) * columnGapMm, dpi);
        var rowHeightDots = MmConversion.ToDots(document.Page.HeightMm, dpi);

        var visibleElements = document.Elements.Where(element => IsVisible(element, layersById)).ToList();

        var rows = new List<ResolvedDocument>();
        for (var start = 0; start < records.Count; start += columns)
        {
            var take = Math.Min(columns, records.Count - start);
            var rowElements = new List<ResolvedElement>();

            for (var column = 0; column < take; column++)
            {
                var recordOptions = new LayoutOptions
                {
                    SampleData = records[start + column],
                    Now = options.Now,
                    ExpressionEngine = options.ExpressionEngine,
                };
                var offsetXDots = MmConversion.ToDots(column * (labelWidthMm + columnGapMm), dpi);
                var visitor = new ElementResolvingVisitor(dpi, recordOptions, layersById, offsetXDots);
                rowElements.AddRange(visibleElements.SelectMany(element => element.Accept(visitor)));
            }

            rows.Add(new ResolvedDocument
            {
                WidthDots = rowWidthDots,
                HeightDots = rowHeightDots,
                Dpi = dpi,
                Elements = rowElements.OrderBy(resolved => resolved.ZIndex).ToList(),
            });
        }

        return rows;
    }

    internal static bool IsVisible(LabelElement element, IReadOnlyDictionary<string, LabelLayer> layersById)
    {
        if (!element.Visible)
        {
            return false;
        }

        return element.LayerId is null
            || !layersById.TryGetValue(element.LayerId, out var layer)
            || layer.Visible;
    }
}
