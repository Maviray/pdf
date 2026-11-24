using Maviray.Pdf.Elements;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Tests.Drawing;
using Maviray.Pdf.Tests.Layout;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Elements;

public sealed class SizeLimitTests
{
    private static readonly RgbColor Blue = new(0, 0, 1);
    private static readonly Size Space = new(495, 742);

    [Fact]
    public void WidthGivesExactlyW()
    {
        var narrowChild = SizeLimit.Width(100, Probe(50, 20)).Measure(Space, Page());
        var (_, calls) = ElementCanvas.Draw(SizeLimit.Width(100, Probe(50, 20)), Space);
        var narrowSpace = SizeLimit.Width(100, Probe(50, 20)).Measure(new Size(80, 742), Page());

        Assert.Equal(new Size(100, 20), narrowChild.Size);
        Assert.Equal(100, calls[0].Width);
        Assert.Equal((Fit.None, "Width"), (narrowSpace.Fit, narrowSpace.Source.Path));
    }

    [Fact]
    public void WidthWithAWiderChildGivesTheChildW()
    {
        var measurement = SizeLimit.Width(100, new FixedBlock("Wide", 150, 20, Blue)).Measure(Space, Page());

        Assert.Equal((Fit.None, "Width > Wide"), (measurement.Fit, measurement.Source.Path));
    }

    [Fact]
    public void MinHeightRaisesEachPagePart()
    {
        // 80 lines of 10: 74 on page 1, and the last 6 on page 2, raised to 100, so the block after starts 100 down.
        var column = new Column([SizeLimit.MinHeight(100, new SplittingBlock("Lines", 100, 10, 80, Blue)), new FixedBlock("After", 200, 20, Blue)], 0);

        var canvas = Generate(column);

        Assert.Equal(150, Assert.Single(canvas.Pages[1].Calls, call => call.Width == 200).Y);
    }

    [Fact]
    public void MaxWidthCapsTheChild()
    {
        var (measurement, calls) = ElementCanvas.Draw(SizeLimit.MaxWidth(100, Probe(50, 20)), Space);

        Assert.Equal(new Size(50, 20), measurement.Size);
        Assert.Equal(100, calls[0].Width);
    }

    [Fact]
    public void MinWidthRaisesTheSizeAndIsNoneInANarrowerSpace()
    {
        Assert.Equal(new Size(100, 20), SizeLimit.MinWidth(100, Probe(50, 20)).Measure(Space, Page()).Size);
        Assert.Equal(Fit.None, SizeLimit.MinWidth(100, Probe(50, 20)).Measure(new Size(80, 742), Page()).Fit);
    }

    [Fact]
    public void MaxHeightGivesAtMostHOnEachPage()
    {
        var canvas = Generate(SizeLimit.MaxHeight(100, new SplittingBlock("Lines", 100, 10, 50, Blue)));

        Assert.Equal(5, canvas.Pages.Count);
        Assert.All(canvas.Pages, page => Assert.Equal(10, page.Calls.Count));
    }

    [Fact]
    public void MinHeightRaisesEachPartAndIsNoneWithLessLeft()
    {
        Assert.Equal(new Size(10, 50), SizeLimit.MinHeight(50, Probe(10, 20)).Measure(Space, Page()).Size);

        var measurement = SizeLimit.MinHeight(50, Probe(10, 20)).Measure(new Size(495, 40), Page());
        Assert.Equal((Fit.None, "MinHeight"), (measurement.Fit, measurement.Source.Path));
    }

    [Fact]
    public void HeightIsBoth()
    {
        Assert.Equal(new Size(10, 50), SizeLimit.Height(50, Probe(10, 20)).Measure(Space, Page()).Size);

        var split = SizeLimit.Height(50, new SplittingBlock("Lines", 10, 10, 10, Blue)).Measure(Space, Page());
        Assert.Equal((Fit.Partial, 50.0), (split.Fit, split.Size.Height));
    }

    [Fact]
    public void AtInfiniteWidthWidthGivesW() =>
        Assert.Equal(100, SizeLimit.Width(100, Probe(50, 20)).Measure(new Size(double.PositiveInfinity, 100), Page()).Size.Width);

    [Fact]
    public void KeepsTheContract()
    {
        Size[] sizes = [new(500, 700), new(150, 250), new(120, 60), new(0, 0)];
        ElementContract.Check(() => SizeLimit.Width(100, Probe(50, 20)), sizes);
        ElementContract.Check(() => SizeLimit.MaxHeight(55, new SplittingBlock("Lines", 40, 10, 30, Blue)), sizes);
        ElementContract.Check(() => SizeLimit.MinHeight(50, Probe(10, 20)), sizes);
    }

    [Fact]
    public void RefusesAMinimumAboveItsMaximum() =>
        Assert.Throws<ArgumentException>(() => new SizeLimit("Bad", 10, 5, 0, double.PositiveInfinity, Probe(1, 1)));

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RefusesABadWidth(double width) => Assert.Throws<ArgumentOutOfRangeException>(() => SizeLimit.Width(width, Probe(1, 1)));

    private static SpaceProbe Probe(double width, double height) => new("Probe", width, height, Blue);

    private static PageContext Page() => new(1, 1, isRenderPass: true);

    private static RecordingCanvas Generate(Element content)
    {
        var canvas = new RecordingCanvas();
        LayoutEngine.Generate([new PageSet(595, 842, new Margins(50, 50, 50, 50), null, null, content, null)], canvas);
        return canvas;
    }
}
