using Maviray.Pdf.Drawing;
using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Tests.Layout;

/// <summary>
/// A test container that gives its child less width than it has, as Padding or a Row item does (P1.10). It passes the
/// child's measurement up, so an error names the child, with the space the child had.
/// </summary>
internal sealed class TestInset : Element
{
    private readonly double _inset;
    private readonly Element _child;

    public TestInset(string name, double inset, Element child)
        : base(name)
    {
        _inset = inset;
        _child = Adopt(child);
    }

    public override Measurement Measure(Size available, PageContext page) => _child.Measure(Inner(available), page);

    public override bool Draw(Size available, ICanvas canvas, PageContext page) => _child.Draw(Inner(available), canvas, page);

    public override void Reset() => _child.Reset();

    private Size Inner(Size available) => new(Math.Max(0, available.Width - _inset), available.Height);
}
