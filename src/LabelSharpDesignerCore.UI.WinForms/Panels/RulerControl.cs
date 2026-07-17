using System.ComponentModel;

namespace LabelSharpDesignerCore.UI.WinForms.Panels;

/// <summary>A horizontal or vertical millimeter ruler, kept in sync with a
/// <see cref="LabelSharpDesignerCore.UI.WinForms.Canvas.LabelCanvasControl"/>'s zoom/pan via
/// <see cref="PixelsPerMm"/>/<see cref="OffsetPx"/> (set by the host from the canvas's
/// <c>ViewChanged</c> event) — this control has no knowledge of the document itself.</summary>
public sealed class RulerControl : Control
{
    private const int MajorEveryMm = 10;
    private const int MinorEveryMm = 1;

    public RulerControl()
    {
        DoubleBuffered = true;
        BackColor = System.Drawing.Color.WhiteSmoke;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Orientation Orientation { get; init; } = Orientation.Horizontal;

    /// <summary>Screen pixels per document millimeter, mirroring the canvas's current zoom.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float PixelsPerMm { get; set; } = 1f;

    /// <summary>Screen-pixel offset of document mm 0 along this ruler's axis, mirroring the canvas's pan.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float OffsetPx { get; set; }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        using var tickPen = new Pen(System.Drawing.Color.Gray);
        using var majorPen = new Pen(System.Drawing.Color.DimGray);
        using var textBrush = new SolidBrush(System.Drawing.Color.DimGray);
        using var font = new Font(Font.FontFamily, 7f);

        var lengthPx = Orientation == Orientation.Horizontal ? Width : Height;
        if (PixelsPerMm <= 0 || lengthPx <= 0)
        {
            return;
        }

        var firstMm = (int)Math.Floor(-OffsetPx / PixelsPerMm) - 1;
        var lastMm = (int)Math.Ceiling((lengthPx - OffsetPx) / PixelsPerMm) + 1;

        for (var mm = firstMm; mm <= lastMm; mm += MinorEveryMm)
        {
            var posPx = mm * PixelsPerMm + OffsetPx;
            var isMajor = mm % MajorEveryMm == 0;
            var tickLength = isMajor ? 8 : 4;

            if (Orientation == Orientation.Horizontal)
            {
                g.DrawLine(isMajor ? majorPen : tickPen, posPx, Height - tickLength, posPx, Height);
                if (isMajor)
                {
                    g.DrawString(mm.ToString(), font, textBrush, posPx + 2, 1);
                }
            }
            else
            {
                g.DrawLine(isMajor ? majorPen : tickPen, Width - tickLength, posPx, Width, posPx);
                if (isMajor)
                {
                    g.TranslateTransform(1, posPx + 2);
                    g.RotateTransform(-90);
                    g.DrawString(mm.ToString(), font, textBrush, 0, 0);
                    g.ResetTransform();
                }
            }
        }
    }
}
