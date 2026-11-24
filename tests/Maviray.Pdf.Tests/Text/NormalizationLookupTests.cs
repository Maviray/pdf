using Maviray.Pdf.Tests.Text.Unicode;
using Maviray.Pdf.Text;

namespace Maviray.Pdf.Tests.Text;

public sealed class NormalizationLookupTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0x110000)]
    [InlineData(int.MaxValue)]
    [InlineData(0x0378)] // unassigned
    [InlineData(0x10FFFF)] // unassigned
    public void LookupsNeverFail(int codePoint)
    {
        Span<int> decomposition = stackalloc int[NormalizationLookup.MaxDecompositionLength];

        Assert.Equal(0, NormalizationLookup.CombiningClass(codePoint));
        Assert.Equal(NfcQuickCheck.Yes, NormalizationLookup.QuickCheck(codePoint));
        Assert.Equal(1, NormalizationLookup.Decompose(codePoint, decomposition));
        Assert.Equal(codePoint, decomposition[0]);
        Assert.Equal(-1, NormalizationLookup.Compose(codePoint, 0x0301));
        Assert.Equal(-1, NormalizationLookup.Compose(0x0041, codePoint));
    }

    // The encoding check: the committed tables against the model the generator wrote them from. This checks how the
    // tables are packed and read, not the Unicode data itself, which NormalizationModelTests pins to the UCD.
    [Fact]
    public void LookupsAgreeWithTheModelForEveryCodePoint()
    {
        var model = NormalizationModel.Load();
        Span<int> decomposition = stackalloc int[NormalizationLookup.MaxDecompositionLength];
        var disagreements = new List<string>();

        for (var codePoint = 0; codePoint < NormalizationModel.CodePointCount; codePoint++)
        {
            if (NormalizationLookup.CombiningClass(codePoint) != model.CombiningClass(codePoint))
            {
                disagreements.Add($"U+{codePoint:X4} combining class");
            }

            if (NormalizationLookup.QuickCheck(codePoint) != model.QuickCheck(codePoint))
            {
                disagreements.Add($"U+{codePoint:X4} quick check");
            }

            if (!Hangul.IsSyllable(codePoint))
            {
                var expected = model.FullDecomposition(codePoint);
                var length = NormalizationLookup.Decompose(codePoint, decomposition);
                var actual = decomposition[..length].ToArray();
                if (!actual.SequenceEqual(expected.Count == 0 ? [codePoint] : expected))
                {
                    disagreements.Add($"U+{codePoint:X4} decomposition");
                }
            }
        }

        foreach (var ((first, second), composite) in model.PrimaryComposites)
        {
            if (NormalizationLookup.Compose(first, second) != composite)
            {
                disagreements.Add($"U+{first:X4} U+{second:X4} composite");
            }
        }

        Assert.Equal(-1, NormalizationLookup.Compose(0x0071, 0x0308));
        Assert.Empty(disagreements.Take(20));
    }

    [Fact]
    public void HangulGoesThroughTheLookups()
    {
        Span<int> decomposition = stackalloc int[NormalizationLookup.MaxDecompositionLength];

        var length = NormalizationLookup.Decompose(0xD4DB, decomposition);

        Assert.Equal(new[] { 0x1111, 0x1171, 0x11B6 }, decomposition[..length].ToArray());
        Assert.Equal(0xAC00, NormalizationLookup.Compose(0x1100, 0x1161));
    }
}
