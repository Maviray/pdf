using Maviray.Pdf.Elements;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Tests.Drawing;
using Maviray.Pdf.Tests.Layout;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Elements;

// Row (spec L-06). A SpaceProbe draws the whole space it is given, so its rectangle shows an item's x, width and height.
public sealed class RowTests
{
    private static readonly RgbColor Blue = new(0, 0, 1);
    private static readonly Size Space = new(495, 742);

    [Fact]
    public void FixedThenAutoThenFlex()
    {
        var row = new Row(
            [Fixed(100, Probe(10, 20)), Auto(Probe(60, 20)), Flex(1, Probe(10, 20)), Flex(3, Probe(10, 20))],
            5);

        var (measurement, calls) = ElementCanvas.Draw(row, Space);

        Assert.Equal((Fit.Full, new Size(495, 20)), (measurement.Fit, measurement.Size));
        Assert.Equal([(0.0, 100.0), (105, 60), (170, 80), (255, 240)], calls.Select(call => (call.X, call.Width)));
    }

    [Fact]
    public void ItemsAreNamedByKindAndPosition()
    {
        var row = new Row([Fixed(50, Probe(10, 10)), Flex(1, new FixedBlock("Big", 10, 800, Blue))], 0);

        var measurement = row.Measure(Space, Page());

        Assert.Equal((Fit.None, "Row > Flex 2 > Big"), (measurement.Fit, measurement.Source.Path));
    }

    [Fact]
    public void ItsHeightIsTheTallestItemsAndEveryItemIsDrawnAtIt()
    {
        var row = new Row([Fixed(50, Probe(50, 30)), Flex(1, Probe(10, 10)), Fixed(2, Probe(2, 0))], 0);

        var (measurement, calls) = ElementCanvas.Draw(row, Space);

        Assert.Equal(30, measurement.Size.Height);
        Assert.All(calls, call => Assert.Equal(30, call.Height));
    }

    [Fact]
    public void AnItemFinishedEarlierDrawsNothingAndItemsKeepTheirPlaces()
    {
        // The first item ends on page 1; the second runs on to page 2, where the row is only its height.
        var row = new Row(
            [Fixed(100, new SplittingBlock("Short", 100, 10, 3, Blue)), Fixed(100, new SplittingBlock("Long", 100, 10, 100, Blue))],
            0);
        var canvas = new RecordingCanvas();

        LayoutEngine.Generate([new PageSet(595, 842, new Margins(50, 50, 50, 50), null, null, row, null)], canvas);

        Assert.Equal(2, canvas.Pages.Count);
        Assert.Equal(3 + 74, canvas.Pages[0].Calls.Count);
        Assert.All(canvas.Pages[1].Calls, call => Assert.Equal(150, call.X));
        Assert.Equal(26, canvas.Pages[1].Calls.Count);
    }

    // A finished auto item measures 0 wide, so without the widths of the first draw, the flex item would move left.
    [Fact]
    public void AFinishedAutoItemKeepsItsWidthOnLaterPages()
    {
        var row = new Row([Auto(new SplittingBlock("Short", 60, 10, 3, Blue)), Flex(1, new SplittingBlock("Long", 100, 10, 100, Blue))], 0);
        var canvas = new RecordingCanvas();

        LayoutEngine.Generate([new PageSet(595, 842, new Margins(50, 50, 50, 50), null, null, row, null)], canvas);

        Assert.All(canvas.Pages[1].Calls, call => Assert.Equal(110, call.X));
    }

    [Fact]
    public void ItIsNoneIfAnyItemIsAndPartialIfAnyItemIs()
    {
        var none = new Row([Fixed(50, Probe(10, 10)), Fixed(50, new FixedBlock("Big", 50, 800, Blue))], 0).Measure(Space, Page());
        var partial = new Row([Fixed(50, new SplittingBlock("Lines", 50, 10, 100, Blue)), Fixed(50, Probe(10, 10))], 0).Measure(Space, Page());

        Assert.Equal((Fit.None, "Row > Fixed 2 > Big"), (none.Fit, none.Source.Path));
        Assert.Equal((Fit.Partial, "Row > Fixed 1 > Lines", 740.0), (partial.Fit, partial.Source.Path, partial.Size.Height));
    }

    [Fact]
    public void FixedAndAutoWiderThanTheSpaceThrows()
    {
        var row = new Row([Fixed(300, Probe(10, 10)), Auto(Probe(250, 10))], 5);

        var error = Assert.Throws<LayoutException>(() => row.Measure(Space, Page()));

        Assert.Equal("Page 1 > Row", error.ElementPath);
        Assert.Equal("Page 1 > Row: its fixed and auto items and spacing need 555 pt, and the space is 495 pt.", error.Message);
        Assert.Equal(495, error.AvailableWidth);
    }

    [Fact]
    public void AtInfiniteWidthFlexItemsMeasureLikeAuto()
    {
        var row = new Row([Fixed(100, Probe(10, 10)), Flex(1, Probe(60, 10))], 5);

        var measurement = row.Measure(new Size(double.PositiveInfinity, 100), Page());

        Assert.Equal((Fit.Full, new Size(165, 10)), (measurement.Fit, measurement.Size));
    }

    // Below 68 points the fixed and auto items and the spacing can never fit, and the Row throws, so no smaller size.
    [Fact]
    public void KeepsTheContract() => ElementContract.Check(
        () => new Row(
            [Fixed(40, new SplittingBlock("A", 40, 12, 30, Blue)), Auto(Probe(20, 50)), Flex(2, new SplittingBlock("B", 10, 7, 50, Blue))],
            4),
        new Size(500, 700), new Size(100, 250), new Size(70, 5));

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RefusesABadFixedWidth(double width) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Row([Fixed(width, Probe(1, 1))], 0));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RefusesABadFlexWeight(double weight) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Row([Flex(weight, Probe(1, 1))], 0));

    [Fact]
    public void RefusesNegativeSpacing() => Assert.Throws<ArgumentOutOfRangeException>(() => new Row([Auto(Probe(1, 1))], -1));

    private static SpaceProbe Probe(double width, double height) => new("Probe", width, height, Blue);

    private static RowItem Fixed(double width, Element element) => new(RowItemKind.Fixed, width, element);

    private static RowItem Flex(double weight, Element element) => new(RowItemKind.Flex, weight, element);

    private static RowItem Auto(Element element) => new(RowItemKind.Auto, 0, element);

    private static PageContext Page() => new(1, 1, isRenderPass: true);
}
