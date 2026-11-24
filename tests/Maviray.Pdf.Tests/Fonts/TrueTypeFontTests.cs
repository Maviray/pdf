using System.Buffers.Binary;
using System.Text;
using Maviray.Pdf.Fonts;

namespace Maviray.Pdf.Tests.Fonts;

public sealed class TrueTypeFontTests
{
    private const string Prefix = "TestFont-Regular: ";

    [Fact]
    public void LoadsTheBuiltFont()
    {
        var font = TrueTypeFont.Load(new TestFontBuilder().Build());

        Assert.Equal("TestFont-Regular", font.PostScriptName);
        Assert.Equal("Test Font", font.FamilyName);
        Assert.Equal("Regular", font.SubfamilyName);
        Assert.Equal(1000, font.UnitsPerEm);
        Assert.Equal(4, font.GlyphCount);
        Assert.Equal(new GlyphBounds(0, -200, 650, 800), font.FontBounds);
        Assert.Equal(0, font.MacStyle);
        Assert.Equal(new LineMetrics(800, -200, 0), font.HorizontalHeaderMetrics);
        Assert.Equal(new LineMetrics(800, -200, 0), font.TypographicMetrics);
        Assert.Equal(new LineMetrics(900, -250, 0), font.WindowsMetrics);
        Assert.Equal(0, font.FsType);
        Assert.Equal(FontEmbedding.Subset, font.Embedding);
        Assert.Equal(400, font.WeightClass);
        Assert.False(font.IsItalic);
        Assert.False(font.IsFixedPitch);
        Assert.Equal(0, font.ItalicAngle);
        Assert.Equal(700, font.CapHeight);
        Assert.Equal(-100, font.UnderlinePosition);
        Assert.Equal(50, font.UnderlineThickness);
        Assert.Equal([500, 250, 600, 650], Enumerable.Range(0, 4).Select(g => font.GetAdvanceWidth((ushort)g)));
        Assert.Equal(2, font.GetGlyphId('A'));
        Assert.Equal(1, font.GetGlyphId(' '));
        Assert.Equal(0, font.GetGlyphId('C'));
        Assert.Equal([(0x20, (ushort)1), (0x41, (ushort)2), (0x42, (ushort)3)], font.CharacterMappings());
        Assert.True(font.TryGetTable("glyf", out var glyf) && glyf.Length > 0);
        Assert.False(font.TryGetTable("fvar", out _));
    }

    [Fact]
    public void ReadsStrikeoutAndTypoMetricsFlag()
    {
        var font = TrueTypeFont.Load(new TestFontBuilder().Strikeout(50, 300).Os2(4, 400, 0x00C0).Build());

        Assert.Equal(50, font.StrikeoutSize);
        Assert.Equal(300, font.StrikeoutPosition);
        Assert.True(font.UseTypographicMetrics);
    }

    [Fact]
    public void NoOs2MeansNoStrikeout()
    {
        var font = TrueTypeFont.Load(new TestFontBuilder().Without("OS/2").Build());

        Assert.Null(font.StrikeoutSize);
        Assert.Null(font.StrikeoutPosition);
        Assert.False(font.UseTypographicMetrics);
    }

    // The values the P1 Text spec quotes for Noto Sans Regular (T-08, T-11).
    [Fact]
    public void NotoSansRegularMatchesTheSpec()
    {
        var font = TestFonts.NotoSans("Regular");

        Assert.Equal(50, font.StrikeoutSize);
        Assert.Equal(322, font.StrikeoutPosition);
        Assert.Equal(-100, font.UnderlinePosition);
        Assert.Equal(50, font.UnderlineThickness);
        Assert.Equal(new LineMetrics(1069, -293, 0), font.TypographicMetrics);
        Assert.True(font.UseTypographicMetrics);
    }

    [Fact]
    public void AdvancesPastNumberOfHMetricsRepeatTheLast()
    {
        var font = TrueTypeFont.Load(new TestFontBuilder().NumberOfHMetrics(2).Build());

        Assert.Equal([500, 250, 250, 250], Enumerable.Range(0, 4).Select(g => font.GetAdvanceWidth((ushort)g)));
    }

    // Glyph 2 is past numberOfHMetrics, so its side bearing comes from hmtx's second array and its advance is the last one.
    [Fact]
    public void LeftSideBearingsComeFromHmtx()
    {
        var font = TrueTypeFont.Load(new TestFontBuilder()
            .ClearGlyphs()
            .SimpleGlyph(500, 50, 0, 450, 700)
            .SimpleGlyph(600, 30, 0, 600, 700)
            .SimpleGlyph(650, -20, 0, 650, 700)
            .NumberOfHMetrics(2)
            .Build());

        Assert.Equal([50, 30, -20], Enumerable.Range(0, 3).Select(g => font.GetLeftSideBearing((ushort)g)));
        Assert.Equal(600, font.GetAdvanceWidth(2));
    }

    [Fact]
    public void LeftSideBearingRefusesAGlyphPastTheFont()
    {
        var font = TrueTypeFont.Load(new TestFontBuilder().Build());

        Assert.Throws<ArgumentOutOfRangeException>(() => font.GetLeftSideBearing(4));
    }

    [Fact]
    public void GlyphDataAndHeaders()
    {
        var font = TrueTypeFont.Load(new TestFontBuilder().Build());

        Assert.True(font.GetGlyphData(1).IsEmpty);
        Assert.Null(font.GetGlyphHeader(1));
        Assert.Equal(new GlyphHeader(1, new GlyphBounds(50, 0, 450, 700)), font.GetGlyphHeader(0));
        Assert.Equal(new GlyphHeader(1, new GlyphBounds(0, 0, 600, 700)), font.GetGlyphHeader(2));
        Assert.Equal(36, font.GetGlyphData(2).Length); // 34 bytes of outline, padded to 4 bytes by the builder; loca covers the padding
        Assert.Throws<ArgumentOutOfRangeException>(() => font.GetAdvanceWidth(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => font.GetGlyphData(4));
    }

    [Fact]
    public void ShortLocaIsDoubled()
    {
        var longLoca = TrueTypeFont.Load(new TestFontBuilder().Build());
        var shortLoca = TrueTypeFont.Load(new TestFontBuilder().IndexToLocFormat(0).Build());

        for (ushort glyph = 0; glyph < 4; glyph++)
        {
            Assert.Equal(longLoca.GetGlyphData(glyph).ToArray(), shortLoca.GetGlyphData(glyph).ToArray());
        }
    }

    [Fact]
    public void RefusesCff()
    {
        var error = Assert.Throws<NotSupportedException>(() => TrueTypeFont.Load(new TestFontBuilder().SfntVersion(0x4F54544F).Build()));

        Assert.StartsWith(Prefix, error.Message, StringComparison.Ordinal);
        Assert.Contains("CFF", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesVariableFonts()
    {
        var error = Assert.Throws<NotSupportedException>(() => TrueTypeFont.Load(new TestFontBuilder().Table("fvar", new byte[16]).Build()));

        Assert.StartsWith(Prefix, error.Message, StringComparison.Ordinal);
        Assert.Contains("variable", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("COLR")]
    [InlineData("CBDT")]
    [InlineData("sbix")]
    [InlineData("SVG ")]
    public void RefusesColourFonts(string tag)
    {
        var error = Assert.Throws<NotSupportedException>(() => TrueTypeFont.Load(new TestFontBuilder().Table(tag, new byte[8]).Build()));

        Assert.StartsWith(Prefix, error.Message, StringComparison.Ordinal);
        Assert.Contains("colour", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesRestrictedFonts()
    {
        var error = Assert.Throws<NotSupportedException>(() => TrueTypeFont.Load(new TestFontBuilder().FsType(0x0002).Build()));

        Assert.StartsWith(Prefix, error.Message, StringComparison.Ordinal);
        Assert.Contains("Restricted License", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("head")]
    [InlineData("hhea")]
    [InlineData("maxp")]
    [InlineData("hmtx")]
    [InlineData("cmap")]
    [InlineData("loca")]
    [InlineData("glyf")]
    public void RefusesAMissingRequiredTable(string tag)
    {
        var error = Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(new TestFontBuilder().Without(tag).Build()));

        Assert.StartsWith(Prefix, error.Message, StringComparison.Ordinal);
        Assert.Contains($"'{tag}'", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("unitsPerEm 15", "head")]
    [InlineData("unitsPerEm 16385", "head")]
    [InlineData("indexToLocFormat 2", "head")]
    [InlineData("numberOfHMetrics 0", "hhea")]
    [InlineData("numberOfHMetrics 5", "hhea")]
    [InlineData("numGlyphs 0", "maxp")]
    [InlineData("short hmtx", "hmtx")]
    [InlineData("decreasing loca", "loca")]
    [InlineData("loca past glyf", "loca")]
    [InlineData("OS/2 version 2 with 86 bytes", "OS/2")]
    [InlineData("short post", "post")]
    [InlineData("name string past the table", "name")]
    public void RefusesBadValues(string damage, string tag)
    {
        var builder = new TestFontBuilder();
        switch (damage)
        {
            case "unitsPerEm 15":
                builder.UnitsPerEm(15);
                break;
            case "unitsPerEm 16385":
                builder.UnitsPerEm(16385);
                break;
            case "indexToLocFormat 2":
                builder.IndexToLocFormat(2);
                break;
            case "numberOfHMetrics 0":
                builder.NumberOfHMetrics(0);
                break;
            case "numberOfHMetrics 5":
                builder.NumberOfHMetrics(5);
                break;
            case "numGlyphs 0":
                builder.Table("maxp", Patched(TableOf(new TestFontBuilder().Build(), "maxp"), 4, 0));
                break;
            case "short hmtx":
                builder.Table("hmtx", new byte[10]);
                break;
            case "decreasing loca":
                builder.Table("loca", LongLoca(0, 36, 0, 72, 108));
                break;
            case "loca past glyf":
                builder.Table("loca", LongLoca(0, 36, 36, 72, 9999));
                break;
            case "OS/2 version 2 with 86 bytes":
                builder.Table("OS/2", TableOf(new TestFontBuilder().Os2(2, 400, 0x40).Build(), "OS/2")[..86]);
                break;
            case "short post":
                builder.Table("post", new byte[20]);
                break;
            case "name string past the table":
                builder.Table("name", Patched(TableOf(new TestFontBuilder().Build(), "name"), 6 + 8, 0xFF00));
                break;
        }

        var error = Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(builder.Build()));

        Assert.Contains($"'{tag}'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutOs2TheStyleComesFromMacStyle()
    {
        var font = TrueTypeFont.Load(new TestFontBuilder().Without("OS/2").MacStyle(3).Build());

        Assert.Equal(700, font.WeightClass);
        Assert.True(font.IsItalic);
        Assert.Equal(0, font.FsType);
        Assert.Equal(FontEmbedding.Subset, font.Embedding);
        Assert.Null(font.TypographicMetrics);
        Assert.Null(font.WindowsMetrics);
        Assert.Null(font.CapHeight);
    }

    [Fact]
    public void WithoutPostTheAngleAndUnderlineAreZero()
    {
        var font = TrueTypeFont.Load(new TestFontBuilder().Without("post").Build());

        Assert.Equal(0, font.ItalicAngle);
        Assert.False(font.IsFixedPitch);
        Assert.Equal(0, font.UnderlinePosition);
        Assert.Equal(0, font.UnderlineThickness);
    }

    [Fact]
    public void ReadsTheItalicAngleAsFixedPoint()
    {
        var font = TrueTypeFont.Load(new TestFontBuilder().Post(-12 * 65536 - 32768, 1).Build());

        Assert.Equal(-12.5, font.ItalicAngle);
        Assert.True(font.IsFixedPitch);
    }

    [Fact]
    public void NameFallsBackToMacintosh()
    {
        var name = NameTable(
            (1, 0, 0, 1, Encoding.Latin1.GetBytes("Caf\u008E")),
            (1, 0, 0, 6, Encoding.Latin1.GetBytes("MacName-Bold")));

        var font = TrueTypeFont.Load(new TestFontBuilder().Table("name", name).Build());

        Assert.Equal("MacName-Bold", font.PostScriptName);
        Assert.Equal("Caf?", font.FamilyName);
        Assert.Null(font.SubfamilyName);
    }

    [Fact]
    public void NamePrefersUsEnglishAndTypographicFamily()
    {
        var name = NameTable(
            (3, 1, 0x407, 1, Encoding.BigEndianUnicode.GetBytes("Deutsch")),
            (3, 1, 0x409, 1, Encoding.BigEndianUnicode.GetBytes("English")),
            (3, 1, 0x407, 6, Encoding.BigEndianUnicode.GetBytes("German-PS")),
            (3, 1, 0x409, 16, Encoding.BigEndianUnicode.GetBytes("Typographic")),
            (3, 1, 0x409, 2, Encoding.BigEndianUnicode.GetBytes("")));

        var font = TrueTypeFont.Load(new TestFontBuilder().Table("name", name).Build());

        Assert.Equal("Typographic", font.FamilyName);
        Assert.Equal("German-PS", font.PostScriptName);
        Assert.Null(font.SubfamilyName); // an empty string counts as absent
    }

    [Fact]
    public void LoadKeepsItsOwnCopy()
    {
        var file = new TestFontBuilder().Build();
        var font = TrueTypeFont.Load(file);
        var glyph = font.GetGlyphData(2).ToArray();

        Array.Clear(file);

        Assert.Equal(2, font.GetGlyphId('A'));
        Assert.Equal(glyph, font.GetGlyphData(2).ToArray());
        Assert.Equal(600, font.GetAdvanceWidth(2));
    }

    [Theory]
    [InlineData("Regular", 3884, 400, false, 0.0, "NotoSans-Regular")]
    [InlineData("Bold", 3884, 700, false, 0.0, "NotoSans-Bold")]
    [InlineData("Italic", 3895, 400, true, -12.0, "NotoSans-Italic")]
    [InlineData("BoldItalic", 3895, 700, true, -12.0, "NotoSans-BoldItalic")]
    public void NotoSansStyles(string style, int glyphCount, int weight, bool italic, double angle, string postScriptName)
    {
        // These values were read from the files with a throwaway script while planning; FontToolsAgreementTests is the
        // independent check of the shared ones.
        var font = TestFonts.NotoSans(style);

        Assert.Equal(1000, font.UnitsPerEm);
        Assert.Equal(new LineMetrics(1069, -293, 0), font.HorizontalHeaderMetrics);
        Assert.Equal(714, font.CapHeight);
        Assert.Equal(0, font.FsType);
        Assert.Equal(FontEmbedding.Subset, font.Embedding);
        Assert.Equal(glyphCount, font.GlyphCount);
        Assert.Equal(weight, font.WeightClass);
        Assert.Equal(italic, font.IsItalic);
        Assert.Equal(angle, font.ItalicAngle);
        Assert.Equal(postScriptName, font.PostScriptName);
        Assert.Equal("Noto Sans", font.FamilyName);
        foreach (var character in "AÄİıƏҐΩ€")
        {
            Assert.True(font.GetGlyphId(character) != 0, $"U+{(int)character:X4} isn't mapped.");
        }

        Assert.Equal(0, font.GetGlyphId(0x4E00));
    }

    internal static byte[] TableOf(byte[] file, string tag) => TableDirectory.Read(file).GetTable(file, tag).ToArray();

    private static byte[] Patched(byte[] table, int offset, ushort value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(offset), value);
        return table;
    }

    private static byte[] LongLoca(params uint[] offsets)
    {
        var bytes = new byte[4 * offsets.Length];
        for (var i = 0; i < offsets.Length; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4 * i), offsets[i]);
        }

        return bytes;
    }

    private static byte[] NameTable(params (ushort Platform, ushort Encoding, ushort Language, ushort NameId, byte[] Text)[] records)
    {
        var header = new List<byte>();
        var strings = new List<byte>();
        TestCmap.Put16(header, 0);
        TestCmap.Put16(header, (ushort)records.Length);
        TestCmap.Put16(header, (ushort)(6 + (12 * records.Length)));
        foreach (var (platform, encoding, language, nameId, text) in records)
        {
            TestCmap.Put16(header, platform);
            TestCmap.Put16(header, encoding);
            TestCmap.Put16(header, language);
            TestCmap.Put16(header, nameId);
            TestCmap.Put16(header, (ushort)text.Length);
            TestCmap.Put16(header, (ushort)strings.Count);
            strings.AddRange(text);
        }

        return [.. header, .. strings];
    }
}
