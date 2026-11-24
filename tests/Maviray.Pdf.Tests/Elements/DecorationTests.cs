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

    [Fact]
    public void ABackgroundFillsTheWidthGivenAndTheChildsHeightFirst()
    {
        var (measurement, calls) = ElementCanvas.Draw(new Background(Grey, new FixedBlock("Block", 50, 20, Blue)), new Size(200, 100));

        Assert.Equal(new Size(50, 20), measurement.Size);
        Assert.Equal([new CanvasCall("Rectangle", 0, 0, 200, 20, Grey), new CanvasCall("Rectangle", 0, 0, 50, 20, Blue)], calls);
    }

    [Fact]
    public void ABackgroundIsOnEveryPagePart()
    {
        var canvas = Generate(new Background(Grey, new SplittingBlock("Lines", 100, 10, 100, Blue)));

        Assert.Equal(new CanvasCall("Rectangle", 50, 50, 495, 740, Grey), canvas.Pages[0].Calls[0]);
        Assert.Equal(new CanvasCall("Rectangle", 50, 50, 495, 260, Grey), canvas.Pages[1].Calls[0]);
    }

    [Fact]
    public void ABorderTakesSpace()
    {
        var (measurement, calls) = ElementCanvas.Draw(new Border(1, 2, 3, 4, Black, new SpaceProbe("Probe", 50, 20, Blue)), new Size(200, 100));

        Assert.Equal(new Size(54, 26), measurement.Size);
        Assert.Equal(new CanvasCall("Rectangle", 1, 2, 196, 94, Blue), calls[0]);
    }

    [Fact]
    public void ABorderDrawsTheChildThenFourBands()
    {
        var (_, calls) = ElementCanvas.Draw(new Border(1, 2, 3, 4, Black, new FixedBlock("Block", 50, 20, Blue)), new Size(200, 100));

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
