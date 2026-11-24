namespace Maviray.Pdf.Tests.Text.Unicode;

// The expected values were read from the UCD 16.0.0 files by a throwaway script, independently of the model, and are
// recorded in the P1.7 plan.
public sealed class CharacterModelTests
{
    private static CharacterModel Model => CharacterModel.Load();

    [Fact]
    public void Counts()
    {
        Assert.Equal(65, Enumerable.Range(0, 0x110000).Count(Model.IsControl));
        Assert.Equal(4174, Enumerable.Range(0, 0x110000).Count(Model.IsDefaultIgnorable));
        Assert.Equal(
            [0x0020, 0x00A0, 0x1680, 0x2000, 0x2001, 0x2002, 0x2003, 0x2004, 0x2005, 0x2006, 0x2007, 0x2008, 0x2009, 0x200A, 0x202F, 0x205F, 0x3000],
            Enumerable.Range(0, 0x110000).Where(Model.IsSpaceSeparator));
    }

    [Theory]
    [InlineData(0x00AD, true)]
    [InlineData(0x034F, true)]
    [InlineData(0x200B, true)]
    [InlineData(0x200D, true)]
    [InlineData(0x2060, true)]
    [InlineData(0xFE0F, true)]
    [InlineData(0xFEFF, true)]
    [InlineData(0x0009, false)]
    [InlineData(0x00A0, false)]
    public void DefaultIgnorables(int codePoint, bool expected)
    {
        Assert.Equal(expected, Model.IsDefaultIgnorable(codePoint));
    }

    [Fact]
    public void Controls()
    {
        Assert.True(Model.IsControl(0x0000));
        Assert.True(Model.IsControl(0x0009));
        Assert.True(Model.IsControl(0x009F));
        Assert.False(Model.IsControl(0x00AD));
    }
}
