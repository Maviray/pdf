using Maviray.Pdf.Elements;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Tests.Drawing;
using Maviray.Pdf.Tests.Layout;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Elements;

public sealed class PaddingTests
{
    private static readonly RgbColor Blue = new(0, 0, 1);
    private static readonly Size Space = new(495, 742);

    [Fact]
    public void TheChildIsInsetAndTheSizeGrows()
    {
        var padding = new Padding(10, 20, 30, 40, new SpaceProbe("Probe", 50, 30, Blue));

        var (measurement, calls) = ElementCanvas.Draw(padding, Space);

        Assert.Equal((Fit.Full, new Size(90, 90)), (measurement.Fit, measurement.Size));
        Assert.Equal(new CanvasCall("Rectangle", 10, 20, 455, 682, Blue), Assert.Single(calls));
    }

    [Fact]
    public void ASpaceBelowZeroIsNone()
    {
        var measurement = new Padding(300, 0, 300, 0, new SpaceProbe("Probe", 10, 10, Blue)).Measure(Space, Page());

        Assert.Equal((Fit.None, "Padding"), (measurement.Fit, measurement.Source.Path));
    }

    [Fact]
    public void AChildWithNoSizeFitsInAZeroSpace()
    {
        var measurement = new Padding(5, 5, 5, 5, new SpaceProbe("Empty", 0, 0, Blue)).Measure(new Size(10, 10), Page());

        Assert.Equal((Fit.Full, new Size(10, 10)), (measurement.Fit, measurement.Size));
    }

    [Fact]
    public void ItSplitsWithItsChildAndInsetsEveryPart()
    {
        var canvas = new RecordingCanvas();
        var padding = new Padding(10, 10, 10, 10, new SplittingBlock("Lines", 100, 10, 100, Blue));

        LayoutEngine.Generate([new PageSet(595, 842, new Margins(50, 50, 50, 50), null, null, padding, null)], canvas);

        Assert.Equal(2, canvas.Pages.Count);
        Assert.Equal(72, canvas.Pages[0].Calls.Count);
        Assert.Equal((60.0, 60.0), (canvas.Pages[1].Calls[0].X, canvas.Pages[1].Calls[0].Y));
    }

    [Fact]
    public void KeepsTheContract() => ElementContract.Check(
        () => new Padding(3, 4, 5, 6, new SplittingBlock("Lines", 40, 12, 30, Blue)),
        new Size(500, 700), new Size(100, 250), new Size(50, 15), new Size(0, 0));

    [Fact]
    public void RefusesNegativePadding() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Padding(0, -1, 0, 0, new SpaceProbe("Probe", 1, 1, Blue)));

    private static PageContext Page() => new(1, 1, isRenderPass: true);
}
