using Maviray.Pdf.Text;

namespace Maviray.Pdf.Tests.Text.Unicode;

// The expected values were read from the UCD 16.0.0 files by a throwaway script, independently of the model, and are
// recorded in the P1.6 plan.
public sealed class LineBreakModelTests
{
    private static LineBreakModel Model => LineBreakModel.Load();

    [Theory]
    [InlineData(0x00A7, "AI", "Po", false)]
    [InlineData(0xD800, "SG", "Cs", false)]
    [InlineData(0x0378, "XX", "Cn", false)]
    [InlineData(0x0E01, "SA", "Lo", false)]
    [InlineData(0x0E31, "SA", "Mn", false)]
    [InlineData(0x102B, "SA", "Mc", false)]
    [InlineData(0x3041, "CJ", "Lo", true)]
    [InlineData(0x000B, "BK", "Cc", false)]
    [InlineData(0x000C, "BK", "Cc", false)]
    [InlineData(0x2028, "BK", "Zl", false)]
    [InlineData(0x2029, "BK", "Zp", false)]
    [InlineData(0x0085, "NL", "Cc", false)]
    [InlineData(0x00AB, "QU", "Pi", false)]
    [InlineData(0x00BB, "QU", "Pf", false)]
    [InlineData(0x201C, "QU", "Pi", false)] // East Asian width A, which isn't F, W or H
    [InlineData(0x201D, "QU", "Pf", false)]
    [InlineData(0x0022, "QU", "Po", false)]
    [InlineData(0x2010, "BA", "Pd", false)]
    [InlineData(0x25CC, "AL", "So", false)]
    [InlineData(0xFFFD, "AI", "So", false)]
    [InlineData(0x3400, "ID", "Lo", true)]
    [InlineData(0x1F466, "EB", "So", true)]
    [InlineData(0x1F3FB, "EM", "Sk", true)]
    [InlineData(0x1FFFD, "ID", "Cn", false)]
    public void RawClassesAndProperties(int codePoint, string lineBreak, string generalCategory, bool eastAsian)
    {
        Assert.Equal(Enum.Parse<LineBreakClass>(lineBreak), Model.RawClass(codePoint));
        Assert.Equal(generalCategory, Model.GeneralCategory(codePoint));
        Assert.Equal(eastAsian, Model.IsEastAsian(codePoint));
    }

    [Theory]
    [InlineData(0x00A7, "AL")]
    [InlineData(0xD800, "AL")]
    [InlineData(0x0378, "AL")]
    [InlineData(0x0E01, "AL")]
    [InlineData(0xFFFD, "AL")]
    [InlineData(0x0E31, "CM")]
    [InlineData(0x102B, "CM")]
    [InlineData(0x3041, "NS")]
    [InlineData(0x0041, "AL")]
    public void Lb1Resolutions(int codePoint, string resolved)
    {
        Assert.Equal(Enum.Parse<LineBreakClass>(resolved), Model.ResolvedClass(codePoint));
    }

    [Fact]
    public void QuotesAndPictographs()
    {
        Assert.True(Model.IsInitialQuote(0x00AB));
        Assert.True(Model.IsInitialQuote(0x201C));
        Assert.True(Model.IsFinalQuote(0x00BB));
        Assert.True(Model.IsFinalQuote(0x201D));
        Assert.False(Model.IsInitialQuote(0x0022));
        Assert.False(Model.IsFinalQuote(0x0022));
        Assert.True(Model.IsExtendedPictographic(0x1F466));
        Assert.True(Model.IsUnassignedPictographic(0x1FFFD));
        Assert.False(Model.IsUnassignedPictographic(0x1F466));
        Assert.Equal(1477, Enumerable.Range(0, 0x110000).Count(Model.IsUnassignedPictographic));
    }

    [Fact]
    public void UsesTheFortyEightClasses()
    {
        var used = Enumerable.Range(0, 0x110000).Select(Model.RawClass).ToHashSet();

        Assert.Equal(48, Enum.GetValues<LineBreakClass>().Length);
        Assert.Equal(Enum.GetValues<LineBreakClass>().ToHashSet(), used);
    }
}
