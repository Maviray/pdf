using Maviray.Pdf.Drawing;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Elements;

/// <summary>
/// Bands of colour around a child, which take space as in CSS (spec L-06). The child gets the space minus the border
/// widths, and the size is the child's plus them. A space below zero is <see cref="Fit.None"/>, as for
/// <see cref="Padding"/>. On every page part it draws the child, then fills the top, bottom, left and right bands of its
/// box: the width it is drawn at, and by default the height it is drawn at, so a Row's cell is framed to the row's
/// height; without stretching, the child's height plus the top and bottom widths (P1.10 plan, ruling 3, amended by the
/// user's decision of 2026-11-24).
/// </summary>
internal sealed class Border : Element
{
    private readonly double _left;
    private readonly double _top;
    private readonly double _right;
    private readonly double _bottom;
    private readonly RgbColor _color;
    private readonly Element _child;
    private readonly bool _stretch;

    /// <param name="left">The left band's width in points, zero or more and finite.</param>
    /// <param name="top">The top band's width in points, zero or more and finite.</param>
    /// <param name="right">The right band's width in points, zero or more and finite.</param>
    /// <param name="bottom">The bottom band's width in points, zero or more and finite.</param>
    /// <param name="color">The bands' colour.</param>
    /// <param name="child">The framed element.</param>
    /// <param name="stretch">Whether the box is the height it is drawn at, or wraps its child.</param>
    public Border(double left, double top, double right, double bottom, RgbColor color, Element child, bool stretch = true)
        : base("Border")
    {
        _stretch = stretch;
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
        if (available.Width - _left - _right < -LayoutTolerance.Points || available.Height - _top - _bottom < -LayoutTolerance.Points)
        {
            return Measurement.None(this, available);
        }

        var measurement = _child.Measure(Inner(available), page);
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
        var height = _stretch ? available.Height : _child.Measure(inner, page).Size.Height + _top + _bottom;
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

    private Size Inner(Size available) => new(Math.Max(0, available.Width - _left - _right), Math.Max(0, available.Height - _top - _bottom));
}
