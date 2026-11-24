using Maviray.Pdf.Layout;
using Maviray.Pdf.Tests.Drawing;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Layout;

// The paging loop and the passes of L-04, on the recording canvas. Pages are 595 × 842 with margins of 50, so the
// inner size is 495 × 742. With a header and a footer of 10, the content has 722.
public sealed class LayoutEngineTests
{
    private const double Width = 595;
    private const double Height = 842;

    private static readonly Margins Margins = new(50, 50, 50, 50);
    private static readonly RgbColor Black = new(0, 0, 0);
    private static readonly RgbColor Grey = new(0.9, 0.9, 0.9);
    private static readonly RgbColor Blue = new(0, 0, 1);

    [Fact]
    public void ATreeOfFixedBlocksPagesCorrectly()
    {
        var canvas = new RecordingCanvas();

        var count = LayoutEngine.Generate([Set(Blocks(30))], canvas);

        Assert.Equal(5, count);
        Assert.Equal(5, canvas.Pages.Count);
        for (var page = 0; page < 5; page++)
        {
            var blocks = canvas.Pages[page].Calls.Where(call => call.Color == Blue).Select(call => (call.X, call.Y)).ToList();
            var expected = Enumerable.Range(0, page < 4 ? 7 : 2).Select(k => (50.0, 60.0 + (100.0 * k))).ToList();
            Assert.Equal(expected, blocks);
        }
    }

    [Fact]
    public void TheHeaderAndFooterAreOnEveryPage()
    {
        var canvas = new RecordingCanvas();
        var header = new SplittingBlock("Head", 495, 5, 2, Black);

        LayoutEngine.Generate([Set(Blocks(30), header, new FixedBlock("Foot", 495, 10, Grey))], canvas);

        Assert.All(canvas.Pages, page =>
        {
            Assert.Equal([(50.0, 50.0), (50.0, 55.0)], page.Calls.Where(call => call.Color == Black).Select(call => (call.X, call.Y)));
            Assert.Equal([(50.0, 782.0)], page.Calls.Where(call => call.Color == Grey).Select(call => (call.X, call.Y)));
        });
    }

    [Fact]
    public void TheBackgroundIsFilledFirst()
    {
        var canvas = new RecordingCanvas();
        var background = new RgbColor(1, 1, 0.8);

        LayoutEngine.Generate([Set(Blocks(10), background: background)], canvas);

        Assert.Equal(2, canvas.Pages.Count);
        Assert.All(canvas.Pages, page => Assert.Equal(new CanvasCall("Rectangle", 0, 0, Width, Height, background), page.Calls[0]));
    }

    [Fact]
    public void ASecondPageSetStartsANewPage()
    {
        var canvas = new RecordingCanvas();
        var landscape = new PageSet(Height, Width, Margins, null, null, Blocks(2), null);

        var count = LayoutEngine.Generate([Set(Blocks(3)), landscape], canvas);

        Assert.Equal(2, count);
        Assert.Equal([(Width, Height), (Height, Width)], canvas.Pages.Select(page => (page.Width, page.Height)));
    }

    [Fact]
    public void PageNumbersCountThroughTheDocument()
    {
        var big = new TestStack("Stack", [new FixedBlock("Big", 300, 800, Blue)]);

        var error = Assert.Throws<LayoutException>(() => LayoutEngine.Generate([Set(Blocks(10)), Set(big)], new RecordingCanvas()));

        Assert.Equal("Page 3 > Content > Stack > Big", error.ElementPath);
    }

    [Fact]
    public void AHeaderThatDoesntFitNamesIt()
    {
        var error = Assert.Throws<LayoutException>(
            () => LayoutEngine.Generate([Set(Blocks(3), header: new FixedBlock("Tall", 495, 800, Black))], new RecordingCanvas()));

        Assert.Equal("Page 1 > Header > Tall", error.ElementPath);
        Assert.Equal((495.0, 742.0), (error.AvailableWidth, error.AvailableHeight));
        Assert.Equal(
            "Page 1 > Header > Tall: the header must fit in full, because it repeats on every page. The space is 495 × 742 pt, and it needs 495 × 800 pt.",
            error.Message);
    }

    [Fact]
    public void AFooterThatDoesntFitNamesIt()
    {
        var error = Assert.Throws<LayoutException>(
            () => LayoutEngine.Generate([Set(Blocks(3), footer: new FixedBlock("Tall", 495, 740, Black))], new RecordingCanvas()));

        Assert.Equal("Page 1 > Footer > Tall", error.ElementPath);
        Assert.Equal((495.0, 732.0), (error.AvailableWidth, error.AvailableHeight));
        Assert.StartsWith("Page 1 > Footer > Tall: the footer must fit in full", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentThatCanNeverFitNamesTheInnermostElement()
    {
        var content = new TestStack("Stack", [.. Enumerable.Range(1, 7).Select(i => new FixedBlock($"Block {i}", 200, 100, Blue)), new FixedBlock("Big", 300, 800, Blue)]);

        var error = Assert.Throws<LayoutException>(() => LayoutEngine.Generate([Set(content)], new RecordingCanvas()));

        Assert.Equal("Page 2 > Content > Stack > Big", error.ElementPath);
        Assert.Equal((495.0, 722.0), (error.AvailableWidth, error.AvailableHeight));
        Assert.Equal("Page 2 > Content > Stack > Big: it can never fit. The space is 495 × 722 pt, and it needs 300 × 800 pt.", error.Message);
    }

    [Fact]
    public void ContentThatSplitsWithoutAdvancingThrows()
    {
        var content = new TestStack("Stack", [new StallingBlock("Stalling")]);

        var error = Assert.Throws<LayoutException>(() => LayoutEngine.Generate([Set(content)], new RecordingCanvas()));

        Assert.Equal("Page 1 > Content > Stack > Stalling", error.ElementPath);
        Assert.StartsWith("Page 1 > Content > Stack > Stalling: it split across pages but drew nothing of itself on this page.", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutAPageCountThereIsOnePass()
    {
        var probe = new PageCountBlock("Probe", _ => 10, showsPageCount: false);

        LayoutEngine.Generate([Set(Blocks(10), footer: probe)], new RecordingCanvas());

        Assert.Equal([(1, 1, true), (2, 2, true)], probe.Shown);
    }

    [Fact]
    public void TheFirstLayoutPassShowsThePageNumber() => Assert.Equal([(1, 1, false), (2, 2, false)], CountedTwoPages()[..2]);

    [Fact]
    public void LaterPassesShowTheCountBefore() => Assert.Equal([(1, 2, false), (2, 2, false)], CountedTwoPages()[2..4]);

    [Fact]
    public void TheRenderPassShowsTheFinalCount() => Assert.Equal([(1, 2, true), (2, 2, true)], CountedTwoPages()[4..]);

    [Fact]
    public void TheTreeIsResetBeforeEveryPass()
    {
        var canvas = new RecordingCanvas();

        LayoutEngine.Generate([Set(Blocks(10), footer: new PageCountBlock("Count", _ => 10))], canvas);

        Assert.Equal([7, 3], canvas.Pages.Select(page => page.Calls.Count(call => call.Color == Blue)));
    }

    [Fact]
    public void ACountThatKeepsChangingTheLayoutThrowsAfterThreePasses()
    {
        // An odd count makes the block 200 high, so the stack needs two pages; an even one makes it 100, so it needs one.
        var content = new TestStack("Stack", [new PageCountBlock("Count", count => count % 2 == 1 ? 200 : 100), new FixedBlock("Block", 200, 600, Blue)]);

        var error = Assert.Throws<LayoutException>(() => LayoutEngine.Generate([Set(content)], new RecordingCanvas()));

        Assert.Equal("Document", error.ElementPath);
        Assert.True(double.IsNaN(error.AvailableWidth) && double.IsNaN(error.AvailableHeight));
        Assert.Equal("Document: the page count keeps changing the layout: the passes ended with 2, 1 and 2 pages.", error.Message);
    }

    private static PageSet Set(Element content, Element? header = null, Element? footer = null, RgbColor? background = null) =>
        new(Width, Height, Margins, background, header ?? new FixedBlock("Head", 495, 10, Grey), content, footer ?? new FixedBlock("Foot", 495, 10, Grey));

    private static TestStack Blocks(int count) =>
        new("Stack", Enumerable.Range(1, count).Select(i => new FixedBlock($"Block {i}", 200, 100, Blue)));

    /// <summary>Two pages of content with a page-count footer: what it was shown in each pass, in order.</summary>
    private static (int Page, int Count, bool Render)[] CountedTwoPages()
    {
        var count = new PageCountBlock("Count", _ => 10);
        LayoutEngine.Generate([Set(Blocks(10), footer: count)], new RecordingCanvas());
        return [.. count.Shown];
    }
}
