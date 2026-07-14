using SkiaSharp;

namespace LabelSharpDesigner.UI.WinForms.Canvas;

/// <summary>Converts between document millimeters and control (screen) pixels.</summary>
internal sealed class CanvasTransform
{
    public float PixelsPerMm { get; set; } = 4f;

    public SKPoint PanOffsetPx { get; set; } = new(24, 24);

    public SKPoint MmToPixels(SKPoint mm) => new(mm.X * PixelsPerMm + PanOffsetPx.X, mm.Y * PixelsPerMm + PanOffsetPx.Y);

    public SKPoint PixelsToMm(SKPoint px) => new((px.X - PanOffsetPx.X) / PixelsPerMm, (px.Y - PanOffsetPx.Y) / PixelsPerMm);

    public float LengthToPixels(float mmLength) => mmLength * PixelsPerMm;

    public SKRect BoundsToPixels(SKRect boundsMm) => SKRect.Create(
        MmToPixels(new SKPoint(boundsMm.Left, boundsMm.Top)),
        new SKSize(boundsMm.Width * PixelsPerMm, boundsMm.Height * PixelsPerMm));

    /// <summary>Recomputes zoom/pan so the whole page fits centered in a control of the given size.</summary>
    public void FitToControl(float pageWidthMm, float pageHeightMm, float controlWidthPx, float controlHeightPx)
    {
        const float marginPx = 24f;
        var availableWidth = Math.Max(controlWidthPx - marginPx * 2, 1);
        var availableHeight = Math.Max(controlHeightPx - marginPx * 2, 1);

        if (pageWidthMm <= 0 || pageHeightMm <= 0)
        {
            return;
        }

        PixelsPerMm = Math.Min(availableWidth / pageWidthMm, availableHeight / pageHeightMm);
        Recenter(pageWidthMm, pageHeightMm, controlWidthPx, controlHeightPx);
    }

    /// <summary>Re-centers the page in a control of the given size without touching the current zoom
    /// level — used on resize once the user has zoomed manually, so the window can be resized without
    /// snapping back to fit-to-control.</summary>
    public void Recenter(float pageWidthMm, float pageHeightMm, float controlWidthPx, float controlHeightPx)
    {
        var pageWidthPx = pageWidthMm * PixelsPerMm;
        var pageHeightPx = pageHeightMm * PixelsPerMm;
        PanOffsetPx = new SKPoint(
            (controlWidthPx - pageWidthPx) / 2f,
            (controlHeightPx - pageHeightPx) / 2f);
    }

    /// <summary>Changes zoom to <paramref name="newPixelsPerMm"/> while keeping the document point
    /// currently under <paramref name="anchorPx"/> fixed on screen — e.g. so mouse-wheel zoom keeps
    /// whatever the cursor was pointing at in place instead of re-centering on the page.</summary>
    public void ZoomAtPoint(float newPixelsPerMm, SKPoint anchorPx)
    {
        var anchorMm = PixelsToMm(anchorPx);
        PixelsPerMm = newPixelsPerMm;
        PanOffsetPx = new SKPoint(anchorPx.X - anchorMm.X * PixelsPerMm, anchorPx.Y - anchorMm.Y * PixelsPerMm);
    }
}
