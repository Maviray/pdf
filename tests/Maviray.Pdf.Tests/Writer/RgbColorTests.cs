using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

public sealed class RgbColorTests
{
    [Fact]
    public void AcceptsComponentsFromZeroToOne()
    {
        var color = new RgbColor(0, 0.5, 1);

        Assert.Equal(0, color.Red);
        Assert.Equal(0.5, color.Green);
        Assert.Equal(1, color.Blue);
    }

    [Theory]
    [InlineData(-0.1, 0.0, 0.0)]
    [InlineData(0.0, 1.1, 0.0)]
    [InlineData(0.0, 0.0, double.NaN)]
    public void RefusesComponentsOutsideZeroToOne(double red, double green, double blue)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RgbColor(red, green, blue));
    }

    [Fact]
    public void EqualColorsAreEqual() => Assert.Equal(new RgbColor(0.2, 0.4, 0.8), new RgbColor(0.2, 0.4, 0.8));
}
