using Maviray.Pdf.Drawing;
using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Tests.Layout;

/// <summary>
/// A test element that throws when it is measured narrower than its width, as a Text element does for a character
/// wider than the space (L-07). A container that measures it at a width it never meant to give fails the test.
/// </summary>
internal sealed class NarrowThrower(string name, double width, double height) : Element(name)
{
    public override Measurement Measure(Size available, PageContext page)
    {
        if (!LayoutTolerance.Fits(width, available.Width))
        {
            throw new InvalidOperationException($"{Path} was measured at a width of {available.Width}, narrower than its {width}.");
        }

        return LayoutTolerance.Fits(height, available.Height) ? Measurement.Full(new Size(width, height), this) : Measurement.None(this, available, new Size(width, height));
    }

    public override bool Draw(Size available, ICanvas canvas, PageContext page) => true;

    public override void Reset()
    {
    }
}
