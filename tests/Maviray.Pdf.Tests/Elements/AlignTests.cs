using Maviray.Pdf.Elements;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Tests.Layout;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Elements;

// The alignment enums are internal, so the theory takes their names.
public sealed class AlignTests
{
    private static readonly RgbColor Blue = new(0, 0, 1);

    [Theory]
    [InlineData("Left", "Top", 0, 0)]
    [InlineData("Center", "Top", 75, 0)]
    [InlineData("Right", "Top", 150, 0)]
    [InlineData("Left", "Middle", 0, 40)]
    [InlineData("Center", "Middle", 75, 40)]
    [InlineData("Right", "Middle", 150, 40)]
    [InlineData("Left", "Bottom", 0, 80)]
    [InlineData("Center", "Bottom", 75, 80)]
    [InlineData("Right", "Bottom", 150, 80)]
    public void ItPlacesTheChildInTheSpaceItIsDrawnIn(string horizontal, string vertical, double x, double y)
    {
        var align = new Align(Enum.Parse<HorizontalAlignment>(horizontal), Enum.Parse<VerticalAlignment>(vertical), new FixedBlock("Block", 50, 20, Blue));

        var (measurement, calls) = ElementCanvas.Draw(align, new Size(200, 100));

        Assert.Equal(new Size(50, 20), measurement.Size);
        Assert.Equal((x, y), (Assert.Single(calls).X, calls[0].Y));
    }

    [Fact]
    public void AnAxisThatIsntSetStaysAtTheStart()
    {
        var (_, calls) = ElementCanvas.Draw(new Align(null, VerticalAlignment.Bottom, new FixedBlock("Block", 50, 20, Blue)), new Size(200, 100));

        Assert.Equal((0.0, 80.0), (calls[0].X, calls[0].Y));
    }

    [Fact]
    public void APartialChildStaysAtTheTop()
    {
        var align = new Align(HorizontalAlignment.Center, VerticalAlignment.Bottom, new SplittingBlock("Lines", 50, 10, 100, Blue));

        var (measurement, calls) = ElementCanvas.Draw(align, new Size(200, 100));

        Assert.Equal(Fit.Partial, measurement.Fit);
        Assert.Equal((75.0, 0.0), (calls[0].X, calls[0].Y));
    }

    [Fact]
    public void AtInfiniteWidthItPlacesTheChildAtZero()
    {
        var (_, calls) = ElementCanvas.Draw(new Align(HorizontalAlignment.Right, null, new FixedBlock("Block", 50, 20, Blue)), new Size(double.PositiveInfinity, 100));

        Assert.Equal(0, calls[0].X);
    }

    // The Column draws each item at its own measured height, so a bottom-aligned item stays where it is.
    [Fact]
    public void InAColumnItAlignsWithinTheItem()
    {
        var column = new Column([new Align(null, VerticalAlignment.Bottom, new FixedBlock("A", 50, 20, Blue)), new FixedBlock("B", 50, 30, Blue)], 0);

        var (_, calls) = ElementCanvas.Draw(column, new Size(495, 742));

        Assert.Equal([0.0, 20], calls.Select(call => call.Y));
    }

    [Fact]
    public void KeepsTheContract() => ElementContract.Check(
        () => new Align(HorizontalAlignment.Center, VerticalAlignment.Middle, new SplittingBlock("Lines", 40, 12, 30, Blue)),
        new Size(500, 700), new Size(100, 250), new Size(50, 15), new Size(0, 0));
}
