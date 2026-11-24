using Maviray.Pdf.Fonts;

namespace Maviray.Pdf.Tests.Fonts;

// CSS Fonts Level 4's weight matching, as L-09 lists it.
public sealed class WeightMatchingTests
{
    [Theory]
    [InlineData(700, new[] { 400, 700 }, 700)]
    [InlineData(400, new[] { 300, 500 }, 500)]
    [InlineData(400, new[] { 200, 600 }, 200)]
    [InlineData(500, new[] { 400, 600 }, 400)]
    [InlineData(450, new[] { 400, 600 }, 400)]
    [InlineData(450, new[] { 480, 400 }, 480)]
    [InlineData(300, new[] { 200, 400 }, 200)]
    [InlineData(300, new[] { 400, 500 }, 400)]
    [InlineData(600, new[] { 500, 700 }, 700)]
    [InlineData(600, new[] { 500, 400 }, 500)]
    [InlineData(100, new[] { 900 }, 900)]
    public void ItChoosesTheWeightCssWould(int desired, int[] available, int expected) =>
        Assert.Equal(expected, WeightMatching.Choose(desired, available));
}
