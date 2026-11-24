using Maviray.Pdf.Layout;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Layout;

public sealed class MeasurementTests
{
    private static readonly FixedBlock Block = new("Block", 10, 20, new RgbColor(0, 0, 0));

    [Fact]
    public void NoneCarriesNoSize()
    {
        var measurement = Measurement.None(Block);

        Assert.Equal(Fit.None, measurement.Fit);
        Assert.Equal(new Size(0, 0), measurement.Size);
        Assert.Null(measurement.Needed);
    }

    [Fact]
    public void NoneKeepsWhatTheElementNeeds()
    {
        var measurement = Measurement.None(Block, new Size(10, 20));

        Assert.Equal(new Size(10, 20), measurement.Needed);
    }

    [Fact]
    public void EachFactoryKeepsItsFitSizeAndSource()
    {
        var full = Measurement.Full(new Size(1, 2), Block);
        var partial = Measurement.Partial(new Size(3, 4), Block);

        Assert.Equal((Fit.Full, new Size(1, 2)), (full.Fit, full.Size));
        Assert.Equal((Fit.Partial, new Size(3, 4)), (partial.Fit, partial.Size));
        Assert.Same(Block, full.Source);
        Assert.Same(Block, partial.Source);
        Assert.Same(Block, Measurement.None(Block).Source);
    }
}
