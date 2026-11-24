using Maviray.Pdf.Drawing;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Elements;

/// <summary>
/// A colour behind a child (spec L-06). It measures as its child. On every page part it fills the width it is drawn at
/// and, by default, the height it is drawn at, so a Row's cell fills the row's height; without stretching, the child's
/// height. Then it draws the child (L-06, amended by the user's decision of 2026-11-24).
/// </summary>
internal sealed class Background : Element
{
    private readonly RgbColor _color;
    private readonly Element _child;
    private readonly bool _stretch;

    /// <param name="color">The colour.</param>
    /// <param name="child">The element in front of it.</param>
    /// <param name="stretch">Whether it fills the height it is drawn at, or only its child's.</param>
    public Background(RgbColor color, Element child, bool stretch = true)
        : base("Background")
    {
        _color = color;
        _child = Adopt(child);
        _stretch = stretch;
    }

    public override bool ShowsPageCount => _child.ShowsPageCount;

    public override Measurement Measure(Size available, PageContext page)
    {
        var measurement = _child.Measure(available, page);
        return measurement.Fit == Fit.Full ? Measurement.Full(measurement.Size, this) : measurement;
    }

    public override bool Draw(Size available, ICanvas canvas, PageContext page)
    {
        var height = _stretch ? available.Height : _child.Measure(available, page).Size.Height;
        canvas.FillRectangle(0, 0, available.Width, height, _color);
        return _child.Draw(available, canvas, page);
    }

    public override void Reset() => _child.Reset();
}
