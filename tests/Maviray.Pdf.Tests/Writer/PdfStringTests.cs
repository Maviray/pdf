using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

public sealed class PdfStringTests
{
    [Fact]
    public void WritesPlainBytesInParentheses() => Assert.Equal("(Hello)", Literal("Hello"u8.ToArray()));

    [Fact]
    public void EscapesParentheses() => Assert.Equal("(a\\(b\\)c)", Literal("a(b)c"u8.ToArray()));

    [Fact]
    public void EscapesAnUnbalancedParenthesis() => Assert.Equal("(\\))", Literal((byte)')'));

    [Fact]
    public void EscapesBackslash() => Assert.Equal("(a\\\\b)", Literal("a\\b"u8.ToArray()));

    [Fact]
    public void EscapesControlCharactersByName() => Assert.Equal("(\\r\\n\\t\\b\\f)", Literal(13, 10, 9, 8, 12));

    [Theory]
    [InlineData((byte)0x01, "(\\001)")]
    [InlineData((byte)0x1F, "(\\037)")]
    [InlineData((byte)0x7F, "(\\177)")]
    public void EscapesOtherControlBytesAsOctal(byte value, string expected)
    {
        Assert.Equal(expected, Literal(value));
    }

    [Fact]
    public void WritesHighBytesAsIs() => Assert.Equal("(éÿ)", Literal(0xE9, 0xFF));

    [Fact]
    public void WritesAnEmptyLiteral() => Assert.Equal("()", Literal());

    [Fact]
    public void WritesHexInUppercasePairs()
    {
        Assert.Equal("<00ABFF>", Serialized.Of(new PdfString(new byte[] { 0x00, 0xAB, 0xFF }, PdfStringForm.Hex)));
        Assert.Equal("<>", Serialized.Of(new PdfString(Array.Empty<byte>(), PdfStringForm.Hex)));
    }

    [Theory]
    [InlineData("Hello", "(Hello)")]
    [InlineData("", "()")]
    [InlineData("Grüße", "<FEFF0047007200FC00DF0065>")] // "Grüße"
    [InlineData("\U0001F600", "<FEFFD83DDE00>")] // a surrogate pair
    [InlineData("a\nb", "<FEFF0061000A0062>")] // a control character isn't printable ASCII
    public void FromTextPicksAsciiOrUtf16(string text, string expected)
    {
        Assert.Equal(expected, Serialized.Of(PdfString.FromText(text)));
    }

    private static string Literal(params byte[] bytes) => Serialized.Of(new PdfString(bytes));
}
