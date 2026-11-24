using Maviray.Pdf.Fluent.Nodes;

namespace Maviray.Pdf.Tests.Fluent;

public sealed class UnitAndPageSizeTests
{
    [Theory]
    [InlineData(10, "Point", 10)]
    [InlineData(1, "Inch", 72)]
    [InlineData(25.4, "Millimetre", 72)]
    [InlineData(2.54, "Centimetre", 72)]
    public void EachUnitConvertsToPoints(double value, string unit, double points) =>
        Assert.Equal(points, Units.ToPoints(value, Enum.Parse<Unit>(unit), "value"), 9);

    [Fact]
    public void AnUndefinedUnitThrows() => Assert.Throws<ArgumentOutOfRangeException>(() => Units.ToPoints(1, (Unit)42, "value"));

    [Fact]
    public void TheIsoSizesAreInPoints()
    {
        Assert.Equal((595.2756, 841.8898), Round(PageSize.A4));
        Assert.Equal((841.8898, 1190.5512), Round(PageSize.A3));
        Assert.Equal((419.5276, 595.2756), Round(PageSize.A5));
    }

    [Fact]
    public void TheUsSizesAreInPoints()
    {
        Assert.Equal((612.0, 792.0), Round(PageSize.Letter));
        Assert.Equal((612.0, 1008.0), Round(PageSize.Legal));
    }

    [Fact]
    public void LandscapePutsTheLongerSideAcross()
    {
        Assert.Equal((841.8898, 595.2756), Round(PageSize.A4.Landscape()));
        Assert.Equal(Round(PageSize.A4.Landscape()), Round(PageSize.A4.Landscape().Landscape()));
    }

    private static (double, double) Round(PageSize size) => (Math.Round(size.Width, 4), Math.Round(size.Height, 4));
}
