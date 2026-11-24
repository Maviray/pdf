using Maviray.Pdf.Drawing;
using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Elements;

/// <summary>
/// Space around a child (spec L-06). The child gets the space minus the padding, offset by the left and top padding,
/// and the size is the child's plus the padding. A space below zero is <see cref="Fit.None"/>. At exactly zero the
/// child is measured, so a child with no size, such as empty text, still fits (P1.10 log, ruling).
/// </summary>
internal sealed class Padding : Element
{
    private readonly double _left;
    private readonly double _top;
    private readonly double _right;
    private readonly double _bottom;
    private readonly Element _child;

    /// <param name="left">The padding on the left in points, zero or more and finite.</param>
    /// <param name="top">The padding at the top in points, zero or more and finite.</param>
    /// <param name="right">The padding on the right in points, zero or more and finite.</param>
    /// <param name="bottom">The padding at the bottom in points, zero or more and finite.</param>
    /// <param name="child">The padded element.</param>
    public Padding(double left, double top, double right, double bottom, Element child)
        : base("Padding")
    {
        _left = Length(left, nameof(left));
        _top = Length(top, nameof(top));
        _right = Length(right, nameof(right));
        _bottom = Length(bottom, nameof(bottom));
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
        canvas.PushOffset(_left, _top);
        var advanced = _child.Draw(Inner(available), canvas, page);
        canvas.PopOffset();
        return advanced;
    }

    public override void Reset() => _child.Reset();

    /// <summary>A length of a padding or border: zero or more, and finite.</summary>
    internal static double Length(double value, string paramName) => double.IsFinite(value) && value >= 0
        ? value
        : throw new ArgumentOutOfRangeException(paramName, value, "The length must be zero or more points, and finite.");

    private Size Inner(Size available) => new(Math.Max(0, available.Width - _left - _right), Math.Max(0, available.Height - _top - _bottom));
}
