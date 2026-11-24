using Maviray.Pdf.Tests.Text.Unicode;
using Maviray.Pdf.Text;

namespace Maviray.Pdf.Tests.Text;

public sealed class CharacterPropertiesTests
{
    // The encoding check: the committed table against the model the generator wrote it from. This checks how the
    // table is packed and read, not the Unicode data itself, which CharacterModelTests pins to the UCD.
    [Fact]
    public void LookupsAgreeWithTheModelForEveryCodePoint()
    {
        var model = CharacterModel.Load();
        var disagreements = new List<string>();

        for (var codePoint = 0; codePoint < 0x110000; codePoint++)
        {
            if (CharacterProperties.IsControl(codePoint) != model.IsControl(codePoint) ||
                CharacterProperties.IsSpaceSeparator(codePoint) != model.IsSpaceSeparator(codePoint) ||
                CharacterProperties.IsDefaultIgnorable(codePoint) != model.IsDefaultIgnorable(codePoint))
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
        Assert.False(CharacterProperties.IsControl(value));
        Assert.False(CharacterProperties.IsSpaceSeparator(value));
        Assert.False(CharacterProperties.IsDefaultIgnorable(value));
    }
}
