using Maviray.Pdf.Fonts;
using Maviray.Pdf.Tests.Fonts;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

/// <summary>
/// The embedded font's name (spec F-09): a six-letter subset tag from SHA-256 of the name and the CIDs' glyphs, then
/// <c>+</c> and the PostScript name, cleaned up so a PDF name can hold it. The expected tags were computed with
/// Python's <c>hashlib</c> while planning.
/// </summary>
public sealed class EmbeddedFontNameTests
{
    public static TheoryData<ushort[], string> Tags => new()
    {
        { [2, 3, 1], "WVQWOF" },
        { [2, 3], "PRUZZI" },
        { [3, 2, 1], "NMFKKE" },
        { [], "KOVWTZ" },
    };

    [Theory]
    [MemberData(nameof(Tags))]
    public void TagsThePostScriptName(ushort[] glyphs, string tag)
    {
        var font = TrueTypeFont.Load(new TestFontBuilder().Build());

        Assert.Equal($"{tag}+TestFont-Regular", EmbeddedFontName.Tagged(font, [0, .. glyphs]));
    }

    public static TheoryData<string?, string?, string?, string> Names => new()
    {
        { "Bad[Name](x){y}<z>/a%b cé", "Test Font", "Regular", "BadNamexyzabc" },
        { null, "Test Font", "Bold", "TestFont-Bold" },
        { null, "Test Font", null, "TestFont" },
        { null, null, null, "Font" },
        { "()[]", "Test Font", "Regular", "Font" },
        { new string('A', 70), "Test Font", "Regular", new string('A', 63) },
    };

    [Theory]
    [MemberData(nameof(Names))]
    public void CleansTheName(string? postScript, string? family, string? subfamily, string expected)
    {
        var font = TrueTypeFont.Load(new TestFontBuilder().Names(postScript, family, subfamily).Build());

        Assert.Equal(expected, EmbeddedFontName.BaseName(font));
    }
}
