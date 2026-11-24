using Maviray.Pdf.Drawing;

namespace Maviray.Pdf.Layout;

/// <summary>
/// A named place in the tree, so a path reads as L-05 shows: a page's <c>Header</c>, <c>Content</c> or <c>Footer</c>
/// (L-04), a Column's <c>Item n</c>, or a Row's <c>Fixed n</c>, <c>Flex n</c> or <c>Auto n</c> (L-06). It measures,
/// draws and resets its child unchanged.
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
