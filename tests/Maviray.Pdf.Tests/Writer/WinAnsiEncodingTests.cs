using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

public sealed class WinAnsiEncodingTests
{
    [Fact]
    public void MapsAsciiToItself()
    {
        for (var code = 0x20; code <= 0x7E; code++)
        {
            Assert.True(WinAnsiEncoding.TryEncode((char)code, out var encoded), $"U+{code:X4}");
            Assert.Equal(code, encoded);
        }
    }

    [Fact]
    public void MapsLatin1ToItself()
    {
        for (var code = 0xA0; code <= 0xFF; code++)
        {
            Assert.True(WinAnsiEncoding.TryEncode((char)code, out var encoded), $"U+{code:X4}");
            Assert.Equal(code, encoded);
        }
    }

    // Annex D of ISO 32000-1: the WinAnsiEncoding codes from 0x80 to 0x9F.
    [Theory]
    [InlineData(0x20AC, 0x80)] // €
    [InlineData(0x201A, 0x82)] // ‚
    [InlineData(0x0192, 0x83)] // ƒ
    [InlineData(0x201E, 0x84)] // „
    [InlineData(0x2026, 0x85)] // …
    [InlineData(0x2020, 0x86)] // †
    [InlineData(0x2021, 0x87)] // ‡
    [InlineData(0x02C6, 0x88)] // ˆ
    [InlineData(0x2030, 0x89)] // ‰
    [InlineData(0x0160, 0x8A)] // Š
    [InlineData(0x2039, 0x8B)] // ‹
    [InlineData(0x0152, 0x8C)] // Œ
    [InlineData(0x017D, 0x8E)] // Ž
    [InlineData(0x2018, 0x91)] // ‘
    [InlineData(0x2019, 0x92)] // ’
    [InlineData(0x201C, 0x93)] // “
    [InlineData(0x201D, 0x94)] // ”
    [InlineData(0x2022, 0x95)] // •
    [InlineData(0x2013, 0x96)] // –
    [InlineData(0x2014, 0x97)] // —
    [InlineData(0x02DC, 0x98)] // ˜
    [InlineData(0x2122, 0x99)] // ™
    [InlineData(0x0161, 0x9A)] // š
    [InlineData(0x203A, 0x9B)] // ›
    [InlineData(0x0153, 0x9C)] // œ
    [InlineData(0x017E, 0x9E)] // ž
    [InlineData(0x0178, 0x9F)] // Ÿ
    public void MapsTheWindows1252Characters(int character, int code)
    {
        Assert.True(WinAnsiEncoding.TryEncode((char)character, out var encoded));
        Assert.Equal(code, encoded);
    }

    [Fact]
    public void NeverProducesUndefinedOrControlCodes()
    {
        byte[] undefined = [0x7F, 0x81, 0x8D, 0x8F, 0x90, 0x9D];
        var encodable = 0;

        for (var character = 0; character <= char.MaxValue; character++)
        {
            if (!WinAnsiEncoding.TryEncode((char)character, out var code))
            {
                continue;
            }

            encodable++;
            Assert.True(code >= 0x20, $"U+{character:X4} became the control code 0x{code:X2}.");
            Assert.DoesNotContain(code, undefined);
        }

        Assert.Equal(27 + 95 + 96, encodable);
    }

    [Theory]
    [InlineData("ğ", 0, "U+011F")] // Turkish g with breve
    [InlineData("ı", 0, "U+0131")] // Turkish dotless i
    [InlineData("Ж", 0, "U+0416")] // Cyrillic Zhe
    [InlineData("\n", 0, "U+000A")]
    [InlineData("ü", 1, "U+0308")] // a decomposed u with diaeresis: no normalization in P0
    public void RefusesCharactersOutsideTheTable(string text, int index, string code)
    {
        var error = Assert.Throws<ArgumentException>(() => WinAnsiEncoding.Encode(text));

        Assert.Contains(code, error.Message, StringComparison.Ordinal);
        Assert.Contains($"index {index}", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EncodesAString()
    {
        byte[] expected = [.. "Gr"u8, 0xFC, 0xDF, .. "e aus K"u8, 0xF6, .. "ln "u8, 0x96, .. " 25 "u8, 0x80];

        Assert.Equal(expected, WinAnsiEncoding.Encode("Grüße aus Köln – 25 €"));
    }
}
