using Maviray.Pdf.Elements;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Tests.Drawing;
using Maviray.Pdf.Tests.Layout;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Elements;

// Background, Border and the lines (spec L-06).
public sealed class DecorationTests
{
    private static readonly RgbColor Blue = new(0, 0, 1);
    private static readonly RgbColor Grey = new(0.9, 0.9, 0.9);
    private static readonly RgbColor Black = new(0, 0, 0);
    private static readonly Size Space = new(495, 742);

    private static readonly Size[] Sizes = [new(500, 700), new(100, 250), new(50, 15), new(0, 0)];

    // By default a background fills the space it is drawn in (L-06, amended by the user's decision of 2026-11-24).
    [Fact]
    public void ABackgroundStretchesToTheSpaceItIsDrawnInFirst()
    {
        var (measurement, calls) = ElementCanvas.Draw(new Background(Grey, new FixedBlock("Block", 50, 20, Blue)), new Size(200, 100));

        Assert.Equal(new Size(50, 20), measurement.Size);
        Assert.Equal([new CanvasCall("Rectangle", 0, 0, 200, 100, Grey), new CanvasCall("Rectangle", 0, 0, 50, 20, Blue)], calls);
    }

    [Fact]
    public void ABackgroundThatDoesntStretchCoversItsChildsHeight()
    {
        var (_, calls) = ElementCanvas.Draw(new Background(Grey, new FixedBlock("Block", 50, 20, Blue), stretch: false), new Size(200, 100));

        Assert.Equal(new CanvasCall("Rectangle", 0, 0, 200, 20, Grey), calls[0]);
    }

    [Fact]
    public void ABackgroundIsOnEveryPagePart()
    {
        var stretched = Generate(new Background(Grey, new SplittingBlock("Lines", 100, 10, 100, Blue)));
        var contained = Generate(new Background(Grey, new SplittingBlock("Lines", 100, 10, 100, Blue), stretch: false));

        Assert.Equal([742.0, 742.0], stretched.Pages.Select(page => page.Calls[0].Height));
        Assert.Equal([740.0, 260.0], contained.Pages.Select(page => page.Calls[0].Height));
    }

    // In a Row, the space is the row's height, so a cell's background lines up with its neighbours.
    [Fact]
    public void ARowCellsBackgroundStretchesToTheRowsHeight()
    {
        RowItem Cell(bool stretch) => new(RowItemKind.Fixed, 100, new Background(Grey, new SpaceProbe("Probe", 10, 10, Blue), stretch));
        RowItem Tall() => new(RowItemKind.Fixed, 100, new SpaceProbe("Tall", 10, 40, Blue));

        var stretched = ElementCanvas.Draw(new Row([Cell(true), Tall()], 0), Space).Calls[0];
        var contained = ElementCanvas.Draw(new Row([Cell(false), Tall()], 0), Space).Calls[0];

        Assert.Equal((100.0, 40.0), (stretched.Width, stretched.Height));
        Assert.Equal((100.0, 10.0), (contained.Width, contained.Height));
    }

    [Fact]
    public void ABorderTakesSpace()
    {
        var (measurement, calls) = ElementCanvas.Draw(new Border(1, 2, 3, 4, Black, new SpaceProbe("Probe", 50, 20, Blue)), new Size(200, 100));

        Assert.Equal(new Size(54, 26), measurement.Size);
        Assert.Equal(new CanvasCall("Rectangle", 1, 2, 196, 94, Blue), calls[0]);
    }

    [Fact]
    public void ABorderDrawsTheChildThenFourBandsAroundTheSpace()
    {
        var (_, calls) = ElementCanvas.Draw(new Border(1, 2, 3, 4, Black, new FixedBlock("Block", 50, 20, Blue)), new Size(200, 100));

        Assert.Equal(
            [
                new CanvasCall("Rectangle", 1, 2, 50, 20, Blue),
                new CanvasCall("Rectangle", 0, 0, 200, 2, Black),
                new CanvasCall("Rectangle", 0, 96, 200, 4, Black),
                new CanvasCall("Rectangle", 0, 0, 1, 100, Black),
                new CanvasCall("Rectangle", 197, 0, 3, 100, Black),
            ],
            calls);
    }

    [Fact]
    public void ABorderThatDoesntStretchWrapsItsChild()
    {
        var (_, calls) = ElementCanvas.Draw(new Border(1, 2, 3, 4, Black, new FixedBlock("Block", 50, 20, Blue), stretch: false), new Size(200, 100));

        Assert.Equal(
            [
                new CanvasCall("Rectangle", 1, 2, 50, 20, Blue),
                new CanvasCall("Rectangle", 0, 0, 200, 2, Black),
                new CanvasCall("Rectangle", 0, 22, 200, 4, Black),
                new CanvasCall("Rectangle", 0, 0, 1, 26, Black),
                new CanvasCall("Rectangle", 197, 0, 3, 26, Black),
            ],
            calls);
    }

    [Fact]
    public void ABorderIsOnEveryPagePart()
    {
        var canvas = Generate(new Border(1, 1, 1, 1, Black, new SplittingBlock("Lines", 100, 10, 100, Blue)));

        Assert.All(canvas.Pages, page => Assert.Equal(4, page.Calls.Count(call => call.Color == Black)));
    }

    [Fact]
    public void ABorderWithASpaceBelowZeroIsNone()
    {
        var measurement = new Border(300, 0, 300, 0, Black, new SpaceProbe("Probe", 1, 1, Blue)).Measure(Space, Page());

        Assert.Equal((Fit.None, "Border"), (measurement.Fit, measurement.Source.Path));
    }

    [Fact]
    public void AHorizontalLineFillsTheWidthGiven()
    {
        var (measurement, calls) = ElementCanvas.Draw(new HorizontalLine(2, Black), new Size(200, 2));

        Assert.Equal((Fit.Full, new Size(0, 2)), (measurement.Fit, measurement.Size));
        Assert.Equal(new CanvasCall("Rectangle", 0, 0, 200, 2, Black), Assert.Single(calls));
    }

    [Fact]
    public void AVerticalLineFillsTheRowsHeight()
    {
        var row = new Row([new RowItem(RowItemKind.Fixed, 50, new SpaceProbe("Probe", 50, 30, Blue)), new RowItem(RowItemKind.Fixed, 2, new VerticalLine(2, Black))], 0);

        var (_, calls) = ElementCanvas.Draw(row, Space);

        Assert.Equal(new CanvasCall("Rectangle", 50, 0, 2, 30, Black), calls[1]);
    }

    [Fact]
    public void ALineThatDoesntFitIsNone()
    {
        Assert.Equal(Fit.None, new HorizontalLine(5, Black).Measure(new Size(100, 3), Page()).Fit);
        Assert.Equal(Fit.None, new VerticalLine(5, Black).Measure(new Size(3, 100), Page()).Fit);
    }

    [Fact]
    public void AtInfiniteWidthAHorizontalLineIsZeroWide() =>
        Assert.Equal(0, new HorizontalLine(2, Black).Measure(new Size(double.PositiveInfinity, 10), Page()).Size.Width);

    [Fact]
    public void TheyKeepTheContract()
    {
        ElementContract.Check(() => new Background(Grey, new SplittingBlock("Lines", 40, 12, 30, Blue)), Sizes);
        ElementContract.Check(() => new Border(1, 2, 3, 4, Black, new SplittingBlock("Lines", 40, 12, 30, Blue)), Sizes);
        ElementContract.Check(() => new HorizontalLine(2, Black), Sizes);
        ElementContract.Check(() => new VerticalLine(2, Black), Sizes);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void TheyRefuseABadThickness(double thickness)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HorizontalLine(thickness, Black));
        Assert.Throws<ArgumentOutOfRangeException>(() => new VerticalLine(thickness, Black));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Border(thickness, 0, 0, 0, Black, new SpaceProbe("Probe", 1, 1, Blue)));
    }

    private static PageContext Page() => new(1, 1, isRenderPass: true);

    private static RecordingCanvas Generate(Element content)
    {
        var canvas = new RecordingCanvas();
        LayoutEngine.Generate([new PageSet(595, 842, new Margins(50, 50, 50, 50), null, null, content, null)], canvas);
        return canvas;
    }
}
