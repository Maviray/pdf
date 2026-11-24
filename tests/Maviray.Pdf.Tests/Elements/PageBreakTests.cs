using Maviray.Pdf.Elements;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Tests.Drawing;
using Maviray.Pdf.Tests.Layout;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Elements;

// The page break (spec L-06), in a Column with spacing, so a gap at the top of the next page would show.
public sealed class PageBreakTests
{
    private static readonly RgbColor Blue = new(0, 0, 1);

    [Fact]
    public void TheContentAfterItStartsANewPageAtTheTop()
    {
        var canvas = Generate(new Column([Block("A"), new PageBreak(), Block("B")], 10));

        Assert.Equal(2, canvas.Pages.Count);
        Assert.Equal([50.0], canvas.Pages[0].Calls.Select(call => call.Y));
        Assert.Equal([50.0], canvas.Pages[1].Calls.Select(call => call.Y));
    }

    [Fact]
    public void TwoBreaksInARowGiveOneEmptyPage()
    {
        var canvas = Generate(new Column([Block("A"), new PageBreak(), new PageBreak(), Block("B")], 10));

        Assert.Equal([1, 0, 1], canvas.Pages.Select(page => page.Calls.Count));
    }

    [Fact]
    public void ItIsPartialOnceThenFullAndResetBringsItBack()
    {
        var page = new PageContext(1, 1, isRenderPass: true);
        var pageBreak = new PageBreak();
        var space = new Size(100, 100);

        var first = pageBreak.Measure(space, page);
        var advanced = pageBreak.Draw(space, NullCanvas(), page);
        var second = pageBreak.Measure(space, page);
        pageBreak.Reset();

        Assert.Equal((Fit.Partial, new Size(0, 0)), (first.Fit, first.Size));
        Assert.True(advanced);
        Assert.Equal((Fit.Full, new Size(0, 0)), (second.Fit, second.Size));
        Assert.Equal(Fit.Partial, pageBreak.Measure(space, page).Fit);
    }

    [Fact]
    public void KeepsTheContract() => ElementContract.Check(() => new PageBreak(), new Size(500, 700), new Size(0, 0));

    private static FixedBlock Block(string name) => new(name, 100, 50, Blue);

    private static Maviray.Pdf.Drawing.ICanvas NullCanvas() => Maviray.Pdf.Drawing.NullCanvas.Instance;

    private static RecordingCanvas Generate(Element content)
    {
        var canvas = new RecordingCanvas();
        LayoutEngine.Generate([new PageSet(595, 842, new Margins(50, 50, 50, 50), null, null, content, null)], canvas);
        return canvas;
    }
}
