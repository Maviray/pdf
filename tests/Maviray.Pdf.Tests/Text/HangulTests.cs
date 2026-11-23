using Maviray.Pdf.Text;

namespace Maviray.Pdf.Tests.Text;

// Expected values from the Unicode Standard 3.12, "Conjoining Jamo Behavior", whose example is U+D4DB.
public sealed class HangulTests
{
    [Theory]
    [InlineData(0xD4DB, new[] { 0x1111, 0x1171, 0x11B6 })]
    [InlineData(0xAC00, new[] { 0x1100, 0x1161 })]
    [InlineData(0xD7A3, new[] { 0x1112, 0x1175, 0x11C2 })]
    public void Decompose(int syllable, int[] expected)
    {
        Span<int> jamo = stackalloc int[3];

        var length = Hangul.Decompose(syllable, jamo);

        Assert.Equal(expected, jamo[..length].ToArray());
    }

    [Theory]
    [InlineData(0x1100, 0x1161, 0xAC00)]
    [InlineData(0xAC00, 0x11A8, 0xAC01)]
    [InlineData(0x1112, 0x1175, 0xD788)]
    [InlineData(0xD788, 0x11C2, 0xD7A3)]
    [InlineData(0xAC00, 0x11A7, -1)] // T starts at U+11A8; U+11A7 is TBase itself
    [InlineData(0xAC01, 0x11A8, -1)] // U+AC01 already has a T
    [InlineData(0x1113, 0x1161, -1)] // L outside U+1100 to U+1112
    [InlineData(0x1100, 0x1176, -1)] // V outside U+1161 to U+1175
    [InlineData(0xD7A4, 0x11A8, -1)] // past the last syllable
    [InlineData(0x0041, 0x030A, -1)] // not Hangul
    public void Compose(int first, int second, int expected)
    {
        Assert.Equal(expected, Hangul.Compose(first, second));
    }

    [Theory]
    [InlineData(0xAC00, true)]
    [InlineData(0xD7A3, true)]
    [InlineData(0xABFF, false)]
    [InlineData(0xD7A4, false)]
    public void IsSyllable(int codePoint, bool expected)
    {
        Assert.Equal(expected, Hangul.IsSyllable(codePoint));
    }
}
