using Maviray.Pdf.Fonts;

namespace Maviray.Pdf.Tests.Fonts;

public sealed class CharacterMapTests
{
    private const int GlyphCount = 10;

    [Fact]
    public void Format4MapsARangeByDelta()
    {
        var map = Format4(new TestSegment(0x41, 0x43, -63));

        Assert.Equal(2, map.GetGlyphId('A'));
        Assert.Equal(3, map.GetGlyphId('B'));
        Assert.Equal(4, map.GetGlyphId('C'));
        Assert.Equal(0, map.GetGlyphId('@'));
        Assert.Equal(0, map.GetGlyphId('D'));
    }

    [Fact]
    public void Format4DeltaWrapsModulo65536()
    {
        var map = Format4(new TestSegment(0xFFF0, 0xFFF0, 20));

        Assert.Equal(4, map.GetGlyphId(0xFFF0));
    }

    [Fact]
    public void Format4MapsThroughTheGlyphArray()
    {
        var map = Format4(new TestSegment(0x61, 0x63, 1, [5, 0, 6]));

        Assert.Equal(6, map.GetGlyphId('a'));
        Assert.Equal(0, map.GetGlyphId('b')); // a 0 in the glyph array stays 0: idDelta isn't added
        Assert.Equal(7, map.GetGlyphId('c'));
    }

    [Fact]
    public void Format4RefusesARangeOffsetPastTheSubtable()
    {
        var cmap = TestCmap.Table((3, 1, TestCmap.Format4(new TestSegment(0x61, 0x63, 0, RangeOffset: 2000))));

        var error = Assert.Throws<InvalidDataException>(() => CharacterMap.Parse(cmap, GlyphCount));

        Assert.Contains("'cmap'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Format4RefusesAMissingFinalSegment()
    {
        var cmap = TestCmap.Table((3, 1, TestCmap.Format4(terminal: false, new TestSegment(0x41, 0x43, -63))));

        Assert.Throws<InvalidDataException>(() => CharacterMap.Parse(cmap, GlyphCount));
    }

    [Fact]
    public void Format4RefusesSegmentsOutOfOrder()
    {
        var cmap = TestCmap.Table((3, 1, TestCmap.Format4(new TestSegment(0x61, 0x63, 1), new TestSegment(0x41, 0x43, 1))));

        Assert.Throws<InvalidDataException>(() => CharacterMap.Parse(cmap, GlyphCount));
    }

    [Fact]
    public void Format4CannotMapAboveTheBmp()
    {
        var map = Format4(new TestSegment(0x41, 0x43, -63));

        Assert.Equal(0, map.GetGlyphId(0x1F600));
    }

    [Fact]
    public void Format12MapsGroups()
    {
        var map = Format12((0x41, 0x43, 2), (0x1F600, 0x1F600, 5));

        Assert.Equal(2, map.GetGlyphId('A'));
        Assert.Equal(4, map.GetGlyphId('C'));
        Assert.Equal(5, map.GetGlyphId(0x1F600));
        Assert.Equal(0, map.GetGlyphId('D'));
        Assert.Equal(0, map.GetGlyphId(0x1F601));
    }

    [Fact]
    public void Format12RefusesOverlappingGroups()
    {
        var cmap = TestCmap.Table((3, 10, TestCmap.Format12((0x41, 0x45, 1), (0x44, 0x50, 1))));

        Assert.Throws<InvalidDataException>(() => CharacterMap.Parse(cmap, GlyphCount));
    }

    [Fact]
    public void Format12RefusesUnsortedGroups()
    {
        var cmap = TestCmap.Table((3, 10, TestCmap.Format12((0x61, 0x62, 1), (0x41, 0x42, 1))));

        Assert.Throws<InvalidDataException>(() => CharacterMap.Parse(cmap, GlyphCount));
    }

    [Fact]
    public void Format12ReturnsZeroForSurrogatesAndBeyondUnicode()
    {
        var map = CharacterMap.Parse(TestCmap.Table((3, 10, TestCmap.Format12((0xD800, 0x10FFFF, 1)))), 65535);

        Assert.Equal(0, map.GetGlyphId(0xD800));
        Assert.Equal(0, map.GetGlyphId(0xDFFF));
        Assert.Equal(0, map.GetGlyphId(0x110000));
        Assert.Equal(0, map.GetGlyphId(-1));
    }

    [Fact]
    public void GlyphsPastTheFontAreUnmapped()
    {
        var format4 = Format4(new TestSegment(0x41, 0x41, 99 - 0x41), new TestSegment(0x42, 0x42, 3 - 0x42));
        var format12 = Format12((0x41, 0x41, 99), (0x42, 0x42, 3));

        foreach (var map in new[] { format4, format12 })
        {
            Assert.Equal(0, map.GetGlyphId('A'));
            Assert.Equal(3, map.GetGlyphId('B'));
            Assert.Equal([(0x42, (ushort)3)], map.Mappings());
        }
    }

    [Fact]
    public void MappingsAreAscendingWithoutNotdef()
    {
        var format4 = Format4(new TestSegment(0x41, 0x43, -63), new TestSegment(0x61, 0x63, 1, [5, 0, 6]));
        var format12 = Format12((0x30, 0x30, 0), (0x41, 0x43, 2), (0x61, 0x61, 6));

        Assert.Equal([(0x41, (ushort)2), (0x42, (ushort)3), (0x43, (ushort)4), (0x61, (ushort)6), (0x63, (ushort)7)], format4.Mappings());
        Assert.Equal([(0x41, (ushort)2), (0x42, (ushort)3), (0x43, (ushort)4), (0x61, (ushort)6)], format12.Mappings());
    }

    [Fact]
    public void PrefersFormat12OverFormat4()
    {
        var cmap = TestCmap.Table(
            (3, 1, TestCmap.Format4(new TestSegment(0x41, 0x41, 2 - 0x41))),
            (3, 10, TestCmap.Format12((0x41, 0x41, 3))));

        Assert.Equal(3, CharacterMap.Parse(cmap, GlyphCount).GetGlyphId('A'));
    }

    [Fact]
    public void FallsBackToTheUnicodePlatform()
    {
        var cmap = TestCmap.Table((0, 3, TestCmap.Format4(new TestSegment(0x41, 0x41, 2 - 0x41))));

        Assert.Equal(2, CharacterMap.Parse(cmap, GlyphCount).GetGlyphId('A'));
    }

    [Fact]
    public void IgnoresAnEncodingInAnotherFormat()
    {
        var cmap = TestCmap.Table(
            (3, 1, TestCmap.Format(6)),
            (0, 3, TestCmap.Format4(new TestSegment(0x41, 0x41, 2 - 0x41))));

        Assert.Equal(2, CharacterMap.Parse(cmap, GlyphCount).GetGlyphId('A'));
    }

    [Fact]
    public void RefusesASymbolOnlyCmap()
    {
        var cmap = TestCmap.Table((3, 0, TestCmap.Format4(new TestSegment(0xF041, 0xF041, 0x0FC1))));

        var error = Assert.Throws<NotSupportedException>(() => CharacterMap.Parse(cmap, GlyphCount));

        Assert.Contains("Unicode", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseSubtableReturnsNullForAnAbsentEncoding()
    {
        var cmap = TestCmap.Table((3, 1, TestCmap.Format4(new TestSegment(0x41, 0x41, 2 - 0x41))));

        Assert.Null(CharacterMap.ParseSubtable(cmap, 3, 10, GlyphCount));
        Assert.NotNull(CharacterMap.ParseSubtable(cmap, 3, 1, GlyphCount));
    }

    // Both subtables come from the font's own build, so agreeing on every BMP code point checks our format 4 reading
    // against our format 12 reading on real data. fontTools checks the result independently (Task 7).
    [Theory]
    [MemberData(nameof(TestFonts.Styles), MemberType = typeof(TestFonts))]
    public void NotoSansFormat4AndFormat12Agree(string style)
    {
        var file = File.ReadAllBytes(TestFonts.NotoSansPath(style));
        var cmap = TableDirectory.Read(file).GetTable(file, "cmap");
        var format4 = CharacterMap.ParseSubtable(cmap, 3, 1, 65535)!;
        var format12 = CharacterMap.ParseSubtable(cmap, 3, 10, 65535)!;

        var mapped = 0;
        for (var codePoint = 0; codePoint <= 0xFFFF; codePoint++)
        {
            var glyph = format12.GetGlyphId(codePoint);
            Assert.True(glyph == format4.GetGlyphId(codePoint), $"U+{codePoint:X4}: format 12 gives {glyph}, format 4 gives {format4.GetGlyphId(codePoint)}.");
            mapped += glyph == 0 ? 0 : 1;
        }

        Assert.True(mapped > 2000, $"Only {mapped} BMP code points are mapped.");
    }

    private static CharacterMap Format4(params TestSegment[] segments) =>
        CharacterMap.Parse(TestCmap.Table((3, 1, TestCmap.Format4(segments))), GlyphCount);

    private static CharacterMap Format12(params (uint Start, uint End, uint StartGlyph)[] groups) =>
        CharacterMap.Parse(TestCmap.Table((3, 10, TestCmap.Format12(groups))), GlyphCount);
}
