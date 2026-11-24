using Maviray.Pdf.Drawing;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Elements;

/// <summary>
/// Bands of colour around a child, which take space as in CSS (spec L-06). The child gets the space minus the border
/// widths, and the size is the child's plus them. A space below zero is <see cref="Fit.None"/>, as for
/// <see cref="Padding"/>. On every page part it draws the child, then fills the top, bottom, left and right bands of its
/// box: the width it is drawn at, and the child's height plus the top and bottom widths (P1.10 plan, ruling 3).
/// </summary>
internal sealed class Border : Element
{
    private readonly double _left;
    private readonly double _top;
    private readonly double _right;
    private readonly double _bottom;
    private readonly RgbColor _color;
    private readonly Element _child;

    public Border(double left, double top, double right, double bottom, RgbColor color, Element child)
        : base("Border")
    {
        _left = Padding.Length(left, nameof(left));
        _top = Padding.Length(top, nameof(top));
        _right = Padding.Length(right, nameof(right));
        _bottom = Padding.Length(bottom, nameof(bottom));
        _color = color;
        _child = Adopt(child);
    }

    public override bool ShowsPageCount => _child.ShowsPageCount;

    public override Measurement Measure(Size available, PageContext page)
    {
        var inner = Inner(available);
        if (inner.Width < 0 || inner.Height < 0)
        {
            return Measurement.None(this, available);
        }

        var measurement = _child.Measure(inner, page);
        if (measurement.Fit == Fit.None)
        {
            return measurement;
        }

        var size = new Size(measurement.Size.Width + _left + _right, measurement.Size.Height + _top + _bottom);
        return measurement.Fit == Fit.Full ? Measurement.Full(size, this) : measurement.WithSize(size);
    }

    public override bool Draw(Size available, ICanvas canvas, PageContext page)
    {
        var inner = Inner(available);
        var height = _child.Measure(inner, page).Size.Height + _top + _bottom;
        canvas.PushOffset(_left, _top);
        var advanced = _child.Draw(inner, canvas, page);
        canvas.PopOffset();

        canvas.FillRectangle(0, 0, available.Width, _top, _color);
        canvas.FillRectangle(0, height - _bottom, available.Width, _bottom, _color);
        canvas.FillRectangle(0, 0, _left, height, _color);
        canvas.FillRectangle(available.Width - _right, 0, _right, height, _color);
        return advanced;
    }

    public override void Reset() => _child.Reset();

    private Size Inner(Size available) => new(available.Width - _left - _right, available.Height - _top - _bottom);
}
