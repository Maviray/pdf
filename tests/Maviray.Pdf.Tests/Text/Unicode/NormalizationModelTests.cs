using Maviray.Pdf.Text;

namespace Maviray.Pdf.Tests.Text.Unicode;

// The expected values were read from the UCD 16.0.0 files by a throwaway script, independently of the model, and are
// recorded in the P1.5 plan.
public sealed class NormalizationModelTests
{
    private static NormalizationModel Model => NormalizationModel.Load();

    [Fact]
    public void CountsMatchTheUcd()
    {
        var decomposable = Enumerable.Range(0, 0x110000).Count(cp => Model.FullDecomposition(cp).Count > 0);
        var quickChecks = Enumerable.Range(0, 0x110000).Select(Model.QuickCheck).ToList();

        Assert.Equal(2081, decomposable);
        Assert.Equal(1120, Model.FullCompositionExclusions.Count);
        Assert.Equal(961, Model.PrimaryComposites.Count);
        // 120 that can join the character before them, and 12 chained composites (Unicode 16.0) whose decomposition
        // starts with one of those, so that they too can join what precedes them.
        Assert.Equal(132, quickChecks.Count(q => q == NfcQuickCheck.Maybe));
        Assert.Equal(1120, quickChecks.Count(q => q == NfcQuickCheck.No));
        Assert.Equal(4, Enumerable.Range(0, 0x110000).Max(cp => Model.FullDecomposition(cp).Count));
    }

    [Theory]
    [InlineData(0x0301, 230)]
    [InlineData(0x0323, 220)]
    [InlineData(0x093C, 7)]
    [InlineData(0x0345, 240)]
    [InlineData(0x1D165, 216)]
    [InlineData(0x0041, 0)]
    [InlineData(0x0378, 0)]
    public void CombiningClasses(int codePoint, int expected)
    {
        Assert.Equal(expected, Model.CombiningClass(codePoint));
    }

    [Theory]
    [InlineData(0x1F87, new[] { 0x03B1, 0x0314, 0x0342, 0x0345 })]
    [InlineData(0x1E08, new[] { 0x0043, 0x0327, 0x0301 })]
    [InlineData(0x212B, new[] { 0x0041, 0x030A })]
    [InlineData(0x1109A, new[] { 0x11099, 0x110BA })]
    [InlineData(0x0041, new int[0])]
    [InlineData(0xAC00, new int[0])]
    public void FullDecompositions(int codePoint, int[] expected)
    {
        Assert.Equal(expected, Model.FullDecomposition(codePoint));
    }

    [Theory]
    [InlineData(0x0958, true)]
    [InlineData(0x212B, true)]
    [InlineData(0x2126, true)]
    [InlineData(0x0344, true)]
    [InlineData(0x0F73, true)]
    [InlineData(0x2ADC, true)]
    [InlineData(0x1D15E, true)]
    [InlineData(0x00C5, false)]
    [InlineData(0x1109A, false)]
    public void Exclusions(int codePoint, bool excluded)
    {
        Assert.Equal(excluded, Model.FullCompositionExclusions.Contains(codePoint));
    }

    [Theory]
    [InlineData(0x0041, 0x030A, 0x00C5)]
    [InlineData(0x0078, 0x0308, 0x1E8D)]
    [InlineData(0x0061, 0x0323, 0x1EA1)]
    [InlineData(0x1EA1, 0x0302, 0x1EAD)]
    [InlineData(0x11099, 0x110BA, 0x1109A)]
    [InlineData(0x0071, 0x0308, -1)]
    [InlineData(0x0915, 0x093C, -1)]
    [InlineData(0x2ADD, 0x0338, -1)]
    [InlineData(0x1EA1, 0x0301, -1)]
    public void Composites(int first, int second, int expected)
    {
        var actual = Model.PrimaryComposites.TryGetValue((first, second), out var composite) ? composite : -1;

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(0x0041, "Yes")]
    [InlineData(0x00C5, "Yes")]
    [InlineData(0xAC00, "Yes")]
    [InlineData(0x0301, "Maybe")]
    [InlineData(0x093C, "Maybe")]
    [InlineData(0x09BE, "Maybe")]
    [InlineData(0x1161, "Maybe")]
    [InlineData(0x11A8, "Maybe")]
    [InlineData(0x113C7, "Maybe")] // chained: U+113C2 U+113B8, and U+1138B U+113C2 composes to U+1138E
    [InlineData(0x16D68, "Maybe")] // chained: U+16D67 U+16D67
    [InlineData(0x16121, "Maybe")] // chained, Gurung Khema
    [InlineData(0x1138E, "Yes")] // composite of U+1138B U+113C2, which starts with a Yes code point
    [InlineData(0x16D69, "Yes")]
    [InlineData(0x0958, "No")]
    [InlineData(0x212B, "No")]
    [InlineData(0x0344, "No")]
    public void QuickCheck(int codePoint, string expected)
    {
        Assert.Equal(Enum.Parse<NfcQuickCheck>(expected), Model.QuickCheck(codePoint));
    }
}
