using Maviray.Pdf.Drawing;
using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Tests.Layout;

/// <summary>
/// A test element that breaks L-03's contract: always <see cref="Fit.Partial"/>, 10 × 1 points, and its draw never
/// advances. The engine must stop on it rather than page forever.
/// </summary>
internal sealed class StallingBlock(string name) : Element(name)
{
    public override Measurement Measure(Size available, PageContext page) => Measurement.Partial(new Size(10, 1), this, available);

    public override bool Draw(Size available, ICanvas canvas, PageContext page) => false;

    public override void Reset()
    {
    }
}
