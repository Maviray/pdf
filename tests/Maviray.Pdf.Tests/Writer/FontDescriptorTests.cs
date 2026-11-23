using Maviray.Pdf.Fonts;
using Maviray.Pdf.Tests.Fonts;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

/// <summary>
/// The font descriptor and the <c>/W</c> array of an embedded font (spec F-09). Font units are converted to the PDF's
/// 1000-unit glyph space and keep four decimals (P0 D-03). The default test font has unitsPerEm 1000, the bounds
/// (0, −200, 650, 800), hhea 800 and −200, an OS/2 cap height of 700 and weight 400.
/// </summary>
public sealed class FontDescriptorTests
{
    [Fact]
    public void DescribesTheDefaultFont()
    {
        var font = Load(new TestFontBuilder());

        Assert.Equal(
            "<</Type /FontDescriptor /FontName /WVQWOF+TestFont-Regular /Flags 4 /FontBBox [0 -200 650 800] /ItalicAngle 0 "
            + "/Ascent 800 /Descent -200 /CapHeight 700 /StemV 96 /FontFile2 7 0 R>>",
            Describe(font));
    }

    [Fact]
    public void ScalesFromUnitsPerEm()
    {
        var text = Describe(Load(new TestFontBuilder().UnitsPerEm(2048)));

        Assert.Contains("/FontBBox [0 -97.6563 317.3828 390.625]", text, StringComparison.Ordinal);
        Assert.Contains("/Ascent 390.625 /Descent -97.6563 /CapHeight 341.7969", text, StringComparison.Ordinal);
    }

    public static TheoryData<string, int, int> Slants => new()
    {
        { "default", 4, 0 },
        { "fixed pitch", 5, 0 },
        { "fsSelection italic", 68, 0 },
        { "macStyle italic", 68, 0 },
        { "italic angle", 68, -12 },
        { "italic angle and fixed pitch", 69, -12 },
    };

    [Theory]
    [MemberData(nameof(Slants))]
    public void FlagsFollowPitchAndSlant(string kind, int flags, int italicAngle)
    {
        var builder = new TestFontBuilder();
        _ = kind switch
        {
            "fixed pitch" => builder.Post(0, 1),
            "fsSelection italic" => builder.Os2(4, 400, 0x01),
            "macStyle italic" => builder.MacStyle(2),
            "italic angle" => builder.Post(-12 << 16, 0),
            "italic angle and fixed pitch" => builder.Post(-12 << 16, 1),
            _ => builder,
        };

        var text = Describe(Load(builder));

        Assert.Contains($"/Flags {flags} ", text, StringComparison.Ordinal);
        Assert.Contains($"/ItalicAngle {italicAngle} ", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData((ushort)400, 96)]
    [InlineData((ushort)700, 169)]
    [InlineData((ushort)900, 218)]
    [InlineData((ushort)0, 22)]
    [InlineData((ushort)1000, 218)]
    public void StemVFollowsTheWeight(ushort weight, int stemV)
    {
        var text = Describe(Load(new TestFontBuilder().Os2(4, weight, 0x40)));

        Assert.Contains($"/StemV {stemV} ", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CapHeightFallsBack()
    {
        var fromH = Describe(Load(new TestFontBuilder().Os2(1, 400, 0x40).SimpleGlyph(600, 0, 0, 600, 720).Map('H', 4)));
        var fromAscent = Describe(Load(new TestFontBuilder().Os2(1, 400, 0x40)));

        Assert.Contains("/CapHeight 720 ", fromH, StringComparison.Ordinal);
        Assert.Contains("/CapHeight 800 ", fromAscent, StringComparison.Ordinal);
    }

    [Fact]
    public void WidthsListEveryCid()
    {
        Assert.Equal("[0 [500 600 650 250]]", Serialized.Of(CidWidths.Create(Load(new TestFontBuilder()), [0, 2, 3, 1])));
        Assert.Equal(
            "[0 [244.1406 292.9688 317.3828 122.0703]]",
            Serialized.Of(CidWidths.Create(Load(new TestFontBuilder().UnitsPerEm(2048)), [0, 2, 3, 1])));
    }

    // The values P1.1 recorded for each style: cap height 714 in 1000 units, weight 400 or 700, italic at −12°.
    public static TheoryData<string, int, int, int> NotoSans => new()
    {
        { "Regular", 4, 96, 0 },
        { "Bold", 4, 169, 0 },
        { "Italic", 68, 96, -12 },
        { "BoldItalic", 68, 169, -12 },
    };

    [Theory]
    [MemberData(nameof(NotoSans))]
    public void NotoSansDescriptors(string style, int flags, int stemV, int italicAngle)
    {
        var text = Describe(TestFonts.NotoSans(style));

        Assert.Contains($"/Flags {flags} ", text, StringComparison.Ordinal);
        Assert.Contains($"/ItalicAngle {italicAngle} ", text, StringComparison.Ordinal);
        Assert.Contains("/CapHeight 714 ", text, StringComparison.Ordinal);
        Assert.Contains($"/StemV {stemV} ", text, StringComparison.Ordinal);
    }

    private static string Describe(TrueTypeFont font) =>
        Serialized.Of(FontDescriptor.Create(font, "WVQWOF+TestFont-Regular", new PdfReference(7)));

    private static TrueTypeFont Load(TestFontBuilder builder) => TrueTypeFont.Load(builder.Build());
}
