using Maviray.Pdf.Drawing;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Layout;

/// <summary>
/// A test element of a natural size that draws one rectangle over the whole space it is drawn in, so a test sees the
/// width and height its container gave it.
/// </summary>
internal sealed class SpaceProbe(string name, double width, double height, RgbColor color) : Element(name)
{
    public override Measurement Measure(Size available, PageContext page) =>
        width <= available.Width && height <= available.Height
            ? Measurement.Full(new Size(width, height), this)
            : Measurement.None(this, available, new Size(width, height));

    public override bool Draw(Size available, ICanvas canvas, PageContext page)
    {
        canvas.FillRectangle(0, 0, available.Width, available.Height, color);
        return true;
    }

    public override void Reset()
    {
    }
}
