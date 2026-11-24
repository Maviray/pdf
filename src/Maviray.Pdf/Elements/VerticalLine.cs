using Maviray.Pdf.Drawing;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Elements;

/// <summary>
/// A vertical rule (spec L-06): width its thickness and height 0, filling the height it is drawn at, which in a Row is
/// the row's height.
/// </summary>
internal sealed class VerticalLine : Element
{
    private readonly double _thickness;
    private readonly RgbColor _color;

    /// <param name="thickness">The line's thickness in points, zero or more and finite.</param>
    /// <param name="color">Its colour.</param>
    public VerticalLine(double thickness, RgbColor color)
        : base("VerticalLine")
    {
        _thickness = Padding.Length(thickness, nameof(thickness));
        _color = color;
    }

    public override Measurement Measure(Size available, PageContext page) => _thickness <= available.Width
        ? Measurement.Full(new Size(_thickness, 0), this)
        : Measurement.None(this, available, new Size(_thickness, 0));

    public override bool Draw(Size available, ICanvas canvas, PageContext page)
    {
        canvas.FillRectangle(0, 0, _thickness, available.Height, _color);
        return true;
    }

    public override void Reset()
    {
    }
}
