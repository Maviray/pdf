using Maviray.Pdf.Drawing;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Elements;

/// <summary>A horizontal rule (spec L-06): width 0 and height its thickness, filling the width it is drawn at.</summary>
internal sealed class HorizontalLine : Element
{
    private readonly double _thickness;
    private readonly RgbColor _color;

    /// <param name="thickness">The line's thickness in points, zero or more and finite.</param>
    /// <param name="color">Its colour.</param>
    public HorizontalLine(double thickness, RgbColor color)
        : base("HorizontalLine")
    {
        _thickness = Padding.Length(thickness, nameof(thickness));
        _color = color;
    }

    public override Measurement Measure(Size available, PageContext page) => _thickness <= available.Height
        ? Measurement.Full(new Size(0, _thickness), this)
        : Measurement.None(this, available, new Size(0, _thickness));

    public override bool Draw(Size available, ICanvas canvas, PageContext page)
    {
        canvas.FillRectangle(0, 0, available.Width, _thickness, _color);
        return true;
    }

    public override void Reset()
    {
    }
}
