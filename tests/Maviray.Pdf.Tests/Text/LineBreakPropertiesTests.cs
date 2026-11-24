using Maviray.Pdf.Tests.Text.Unicode;
using Maviray.Pdf.Text;

namespace Maviray.Pdf.Tests.Text;

public sealed class LineBreakPropertiesTests
{
    // The encoding check: the committed table against the model the generator wrote it from. This checks how the
    // table is packed and read, not the Unicode data itself, which LineBreakModelTests pins to the UCD.
    [Fact]
    public void LookupsAgreeWithTheModelForEveryCodePoint()
    {
        var model = LineBreakModel.Load();
        var disagreements = new List<string>();

        for (var codePoint = 0; codePoint < 0x110000; codePoint++)
        {
            if (LineBreakProperties.Class(codePoint) != model.ResolvedClass(codePoint) ||
                LineBreakProperties.IsEastAsian(codePoint) != model.IsEastAsian(codePoint) ||
                LineBreakProperties.IsInitialQuote(codePoint) != model.IsInitialQuote(codePoint) ||
                LineBreakProperties.IsFinalQuote(codePoint) != model.IsFinalQuote(codePoint) ||
                LineBreakProperties.IsUnassignedPictographic(codePoint) != model.IsUnassignedPictographic(codePoint))
            {
                disagreements.Add($"U+{codePoint:X4}");
            }
        }

        Assert.Empty(disagreements.Take(20));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0x110000)]
    [InlineData(int.MaxValue)]
    public void LookupsNeverFail(int value)
    {
        Assert.Equal(LineBreakClass.AL, LineBreakProperties.Class(value));
        Assert.False(LineBreakProperties.IsEastAsian(value));
        Assert.False(LineBreakProperties.IsInitialQuote(value));
        Assert.False(LineBreakProperties.IsFinalQuote(value));
        Assert.False(LineBreakProperties.IsUnassignedPictographic(value));
    }
}
