using Maviray.Pdf.Drawing;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Elements;

/// <summary>
/// A colour behind a child (spec L-06). It measures as its child. On every page part it fills the width it is drawn at
/// and the child's height, then draws the child.
/// </summary>
internal sealed class Background : Element
{
    private readonly RgbColor _color;
    private readonly Element _child;

    public Background(RgbColor color, Element child)
        : base("Background")
    {
        _color = color;
        _child = Adopt(child);
    }

    public override bool ShowsPageCount => _child.ShowsPageCount;

    public override Measurement Measure(Size available, PageContext page)
    {
        var measurement = _child.Measure(available, page);
        return measurement.Fit == Fit.Full ? Measurement.Full(measurement.Size, this) : measurement;
    }

    public override bool Draw(Size available, ICanvas canvas, PageContext page)
    {
        var height = _child.Measure(available, page).Size.Height;
        canvas.FillRectangle(0, 0, available.Width, height, _color);
        return _child.Draw(available, canvas, page);
    }

    public override void Reset() => _child.Reset();
}
