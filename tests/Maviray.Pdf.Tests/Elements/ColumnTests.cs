using Maviray.Pdf.Elements;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Tests.Drawing;
using Maviray.Pdf.Tests.Layout;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Elements;

// Column (spec L-06). Pages through LayoutEngine are 595 x 842 with margins of 50 and no header or footer, so the
// content is 495 x 742 at (50, 50).
public sealed class ColumnTests
{
    private static readonly RgbColor Blue = new(0, 0, 1);
    private static readonly Size Space = new(495, 742);

    [Fact]
    public void ItemsStackWithSpacing()
    {
        var column = new Column([Block("A", 100, 50), Block("B", 100, 30), Block("C", 100, 20)], 10);

        var (measurement, calls) = ElementCanvas.Draw(column, Space);

        Assert.Equal((Fit.Full, new Size(100, 120)), (measurement.Fit, measurement.Size));
        Assert.Equal([0.0, 60, 100], calls.Select(call => call.Y));
    }

    [Fact]
    public void ItsWidthIsTheWidestItems()
    {
        var column = new Column([Block("A", 100, 10), Block("B", 300, 10), Block("C", 200, 10)], 0);

        Assert.Equal(300, ElementCanvas.Draw(column, Space).Measurement.Size.Width);
    }

    [Fact]
    public void ItSplitsAtTheFirstItemThatDoesntFitAndSpacingDoesntCarryOver()
    {
        // Six items of 100 with spacing 10 take 650 points; a seventh would end at 760, past 742.
        var canvas = Generate(new Column(Enumerable.Range(1, 8).Select(i => Block($"Block {i}", 100, 100)), 10));

        Assert.Equal(2, canvas.Pages.Count);
        Assert.Equal([50.0, 160, 270, 380, 490, 600], canvas.Pages[0].Calls.Select(call => call.Y));
        Assert.Equal([50.0, 160], canvas.Pages[1].Calls.Select(call => call.Y));
    }

    [Fact]
    public void ASplittingItemContinuesOnTheNextPage()
    {
        var canvas = Generate(new Column([Block("Tall", 100, 700), new SplittingBlock("Lines", 100, 10, 10, Blue)], 0));

        Assert.Equal(5, canvas.Pages[0].Calls.Count);
        Assert.Equal([50.0, 60, 70, 80, 90, 100], canvas.Pages[1].Calls.Select(call => call.Y));
    }

    [Fact]
    public void ItIsNoneOnlyWhenItsFirstRemainingItemIs()
    {
        var page = new PageContext(1, 1, isRenderPass: true);
        var alone = new Column([Block("Big", 100, 800)], 0);
        var after = new Column([Block("A", 100, 100), Block("Big", 100, 800)], 0);

        var none = alone.Measure(Space, page);
        var partial = after.Measure(Space, page);

        Assert.Equal((Fit.None, "Column > Item 1 > Big"), (none.Fit, none.Source.Path));
        Assert.Equal((Fit.Partial, new Size(100, 100), "Column"), (partial.Fit, partial.Size, partial.Source.Path));
    }

    [Fact]
    public void AnItemWithNoHeightTakesNoSpacing()
    {
        var column = new Column([Block("A", 100, 50), Block("Empty", 100, 0), Block("B", 100, 50)], 10);

        var (measurement, calls) = ElementCanvas.Draw(column, Space);

        Assert.Equal(110, measurement.Size.Height);
        Assert.Equal([0.0, 50, 60], calls.Select(call => call.Y));
    }

    [Fact]
    public void FinishedItemsNeverDrawAgain()
    {
        var canvas = Generate(new Column([Block("First", 100, 700), Block("Second", 200, 100)], 0));

        Assert.Equal([100.0], canvas.Pages[0].Calls.Select(call => call.Width));
        Assert.Equal([200.0], canvas.Pages[1].Calls.Select(call => call.Width));
    }

    [Fact]
    public void KeepsTheContract() => ElementContract.Check(
        () => new Column(
            [Block("A", 80, 100), new SplittingBlock("B", 100, 12, 30, Blue), new Column([Block("C", 90, 60), Block("D", 90, 0)], 3)],
            5),
        new Size(500, 700), new Size(100, 250), new Size(50, 5), new Size(0, 0));

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RefusesBadSpacing(double spacing) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Column([Block("A", 1, 1)], spacing));

    private static FixedBlock Block(string name, double width, double height) => new(name, width, height, Blue);

    private static RecordingCanvas Generate(Element content)
    {
        var canvas = new RecordingCanvas();
        LayoutEngine.Generate([new PageSet(595, 842, new Margins(50, 50, 50, 50), null, null, content, null)], canvas);
        return canvas;
    }
}
