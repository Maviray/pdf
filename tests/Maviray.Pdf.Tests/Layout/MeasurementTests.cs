using Maviray.Pdf.Layout;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Layout;

public sealed class MeasurementTests
{
    private static readonly FixedBlock Block = new("Block", 10, 20, new RgbColor(0, 0, 0));

    [Fact]
    public void NoneCarriesNoSize()
    {
        var measurement = Measurement.None(Block, new Size(5, 5));

        Assert.Equal(Fit.None, measurement.Fit);
        Assert.Equal(new Size(0, 0), measurement.Size);
        Assert.Null(measurement.Needed);
    }

    [Fact]
    public void NoneKeepsTheSpaceAndWhatTheElementNeeds()
    {
        var measurement = Measurement.None(Block, new Size(5, 5), new Size(10, 20));

        Assert.Equal(new Size(5, 5), measurement.Space);
        Assert.Equal(new Size(10, 20), measurement.Needed);
    }

    [Fact]
    public void EachFactoryKeepsItsFitSizeAndSource()
    {
        var full = Measurement.Full(new Size(1, 2), Block);
        var partial = Measurement.Partial(new Size(3, 4), Block, new Size(30, 4));

        Assert.Equal((Fit.Full, new Size(1, 2)), (full.Fit, full.Size));
        Assert.Equal((Fit.Partial, new Size(3, 4), new Size(30, 4)), (partial.Fit, partial.Size, partial.Space));
        Assert.Same(Block, full.Source);
        Assert.Same(Block, partial.Source);
        Assert.Same(Block, Measurement.None(Block, default).Source);
    }

    [Fact]
    public void WithSizeKeepsTheSourceAndItsSpace()
    {
        var passedUp = Measurement.Partial(new Size(3, 4), Block, new Size(30, 4)).WithSize(new Size(50, 60));

        Assert.Equal((Fit.Partial, new Size(50, 60), new Size(30, 4)), (passedUp.Fit, passedUp.Size, passedUp.Space));
        Assert.Same(Block, passedUp.Source);
    }
}
