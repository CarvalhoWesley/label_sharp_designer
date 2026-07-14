using System.ComponentModel;
using LabelSharpDesigner.Core.Layout;
using LabelSharpDesigner.Rendering.Canvas;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace LabelSharpDesigner.App;

/// <summary>Fit-to-control preview of a <see cref="ResolvedDocument"/>, reusing
/// <see cref="LabelCanvasRenderer"/> — the same "real" renderer used for PNG export — so the export
/// dialog's preview can never visually diverge from the actual PNG output.</summary>
internal sealed class RenderPreviewControl : SKControl
{
    public RenderPreviewControl()
    {
        DoubleBuffered = true;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ResolvedDocument? Document { get; set; }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);

        var canvas = e.Surface.Canvas;
        canvas.Clear(new SKColor(0x3A, 0x3D, 0x41));

        var document = Document;
        if (document is null || document.WidthDots <= 0 || document.HeightDots <= 0)
        {
            return;
        }

        const float marginPx = 16f;
        var availableWidth = Math.Max(Width - marginPx * 2, 1);
        var availableHeight = Math.Max(Height - marginPx * 2, 1);
        var scale = Math.Min(availableWidth / document.WidthDots, availableHeight / document.HeightDots);
        var offsetX = (Width - document.WidthDots * scale) / 2f;
        var offsetY = (Height - document.HeightDots * scale) / 2f;

        canvas.Save();
        canvas.Translate(offsetX, offsetY);
        canvas.Scale(scale);
        LabelCanvasRenderer.Render(canvas, document);
        canvas.Restore();
    }
}
