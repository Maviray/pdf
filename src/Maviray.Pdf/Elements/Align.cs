using Maviray.Pdf.Drawing;
using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Elements;

/// <summary>
/// A child placed in the space it is drawn in (spec L-06). It measures as its child, at the space, and draws the child
/// at its measured size: left, centre or right across, and top, middle or bottom down. A child that splits stays at the
/// top. An axis that isn't set stays at the start, and at an infinite width the child sits at x = 0, because there is no
/// right edge (P1.10 plan, ruling 5).
/// </summary>
internal sealed class Align : Element
{
    private readonly HorizontalAlignment? _horizontal;
    private readonly VerticalAlignment? _vertical;
    private readonly Element _child;

    public Align(HorizontalAlignment? horizontal, VerticalAlignment? vertical, Element child)
        : base("Align")
    {
        _horizontal = horizontal;
        _vertical = vertical;
        _child = Adopt(child);
    }

    public override bool ShowsPageCount => _child.ShowsPageCount;

    public override Measurement Measure(Size available, PageContext page) => _child.Measure(available, page);

    public override bool Draw(Size available, ICanvas canvas, PageContext page)
    {
        var measurement = _child.Measure(available, page);
        var size = measurement.Size;
        var x = double.IsFinite(available.Width)
            ? _horizontal switch
            {
                HorizontalAlignment.Center => (available.Width - size.Width) / 2,
                HorizontalAlignment.Right => available.Width - size.Width,
                _ => 0,
            }
            : 0;
        var y = measurement.Fit == Fit.Partial
            ? 0
            : _vertical switch
            {
                VerticalAlignment.Middle => (available.Height - size.Height) / 2,
                VerticalAlignment.Bottom => available.Height - size.Height,
                _ => 0,
            };

        canvas.PushOffset(Math.Max(0, x), Math.Max(0, y));
        var advanced = _child.Draw(size, canvas, page);
        canvas.PopOffset();
        return advanced;
    }

    public override void Reset() => _child.Reset();
}
