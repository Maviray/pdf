using Maviray.Pdf.Drawing;

namespace Maviray.Pdf.Layout;

/// <summary>
/// A page's header, content or footer (spec L-04): the top of its tree, named <c>Header</c>, <c>Content</c> or
/// <c>Footer</c> in paths (L-05). It measures, draws and resets its child unchanged.
/// </summary>
internal sealed class Slot : Element
{
    private readonly Element _child;

    public Slot(string name, Element child)
        : base(name)
    {
        _child = Adopt(child);
    }

    public override bool ShowsPageCount => _child.ShowsPageCount;

    public override Measurement Measure(Size available, PageContext page) => _child.Measure(available, page);

    public override bool Draw(Size available, ICanvas canvas, PageContext page) => _child.Draw(available, canvas, page);

    public override void Reset() => _child.Reset();
}
