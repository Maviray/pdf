using Maviray.Pdf.Drawing;
using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Elements;

/// <summary>
/// The end of a page (spec L-06): <see cref="Fit.Partial"/> with no size the first time, so the content ends on this
/// page, and then <see cref="Fit.Full"/> with no size. Its draw marks it passed, which counts as progress.
/// </summary>
internal sealed class PageBreak : Element
{
    private bool _passed;

    public PageBreak()
        : base("PageBreak")
    {
    }

    public override Measurement Measure(Size available, PageContext page) =>
        _passed ? Measurement.Full(default, this) : Measurement.Partial(default, this, available);

    public override bool Draw(Size available, ICanvas canvas, PageContext page)
    {
        _passed = true;
        return true;
    }

    public override void Reset() => _passed = false;
}
