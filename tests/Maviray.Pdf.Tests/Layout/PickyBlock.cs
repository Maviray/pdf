using Maviray.Pdf.Drawing;
using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Tests.Layout;

/// <summary>
/// A test element that breaks the contract's measured-size rule: it is <see cref="Fit.Full"/> at its size only with one
/// point to spare, so measured again at its own measured size it is <see cref="Fit.None"/>.
/// </summary>
internal sealed class PickyBlock(string name, double width, double height) : Element(name)
{
    public override Measurement Measure(Size available, PageContext page) =>
        available.Width >= width && available.Height >= height + 1
            ? Measurement.Full(new Size(width, height), this)
            : Measurement.None(this, available, new Size(width, height + 1));

    public override bool Draw(Size available, ICanvas canvas, PageContext page) => true;

    public override void Reset()
    {
    }
}
