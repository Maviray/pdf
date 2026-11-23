using System.Buffers.Binary;
using Maviray.Pdf.Fonts;

namespace Maviray.Pdf.Tests.Fonts;

/// <summary>
/// The subset file (spec F-08 steps 2 to 6): glyphs copied in CID order with their component IDs rewritten, padded to
/// 4 bytes, one full metric per glyph, the header tables copied with their counts changed, the hinting tables kept and
/// everything else dropped. The default test font has glyphs 0 to 3, with 2 and 3 simple; composites start at glyph 4.
/// </summary>
public sealed class FontSubsetterTests
{
    private static readonly string[] HintedTags = ["cvt ", "fpgm", "gasp", "glyf", "head", "hhea", "hmtx", "loca", "maxp", "post", "prep"];

    [Fact]
    public void GlyphsAreCopiedInSubsetOrder()
    {
        var font = Load(new TestFontBuilder());

        var subset = FontSubsetter.Build(font, [0, 3, 2]);
        var file = Read(subset);

        Assert.Equal((ushort[])[0, 3, 2], subset.OriginalGlyphIds);
        Assert.Equal(3, file.GlyphCount);
        Assert.Equal(font.GetGlyphData(0).ToArray(), file.Glyph(0));
        Assert.Equal(font.GetGlyphData(3).ToArray(), file.Glyph(1));
        Assert.Equal(font.GetGlyphData(2).ToArray(), file.Glyph(2));
    }

    [Fact]
    public void AGlyphUsedTwiceIsCopiedTwice()
    {
        var file = Read(FontSubsetter.Build(Load(new TestFontBuilder()), [0, 2, 2]));

        Assert.Equal(file.Glyph(1), file.Glyph(2));
        Assert.NotEqual(file.Loca[1], file.Loca[2]);
    }

    [Fact]
    public void ComponentIdsAreRewritten()
    {
        var font = Load(new TestFontBuilder().CompositeGlyph(700, C(2, 3)).CompositeGlyph(700, C(4, 2)));

        var subset = FontSubsetter.Build(font, [0, 5, 3]);
        var file = Read(subset);

        Assert.Equal((ushort[])[0, 5, 3, 4, 2], subset.OriginalGlyphIds);
        Assert.Equal((ushort[])[3, 4], file.ComponentIds(1));
        Assert.Equal((ushort[])[4, 2], file.ComponentIds(3));
        AssertSameGlyphsOnceMappedBack(font, subset);
    }

    [Theory]
    [InlineData((ushort)0x0003)]
    [InlineData((ushort)0x000A)]
    [InlineData((ushort)0x0042)]
    [InlineData((ushort)0x0082)]
    [InlineData((ushort)0x0102)]
    public void EveryRecordLengthIsRewritten(ushort flags)
    {
        var font = Load(new TestFontBuilder().CompositeGlyph(700, new TestComponent(2, flags), new TestComponent(3, flags)));

        var subset = FontSubsetter.Build(font, [0, 4, 3]);

        Assert.Equal((ushort[])[0, 4, 3, 2], subset.OriginalGlyphIds);
        Assert.Equal((ushort[])[3, 2], Read(subset).ComponentIds(1));
        AssertSameGlyphsOnceMappedBack(font, subset);
    }

    [Fact]
    public void GlyphDataIsPaddedToFourBytes()
    {
        var font = Load(new TestFontBuilder().GlyphAlignment(1));

        var file = Read(FontSubsetter.Build(font, [0, 2, 3]));

        Assert.All(file.Loca, offset => Assert.Equal(0u, offset % 4));
        foreach (var (subsetGlyph, original) in new[] { (0, 0), (1, 2), (2, 3) })
        {
            Assert.Equal(34, font.GetGlyphData((ushort)original).Length);
            Assert.Equal([.. font.GetGlyphData((ushort)original), 0, 0], file.Glyph(subsetGlyph));
        }
    }

    [Fact]
    public void MetricsHaveOneFullEntryPerGlyph()
    {
        var font = Load(new TestFontBuilder()
            .ClearGlyphs()
            .SimpleGlyph(500, 50, 0, 450, 700)
            .SimpleGlyph(600, 30, 0, 600, 700)
            .SimpleGlyph(650, -20, 0, 650, 700)
            .NumberOfHMetrics(2));

        var file = Read(FontSubsetter.Build(font, [0, 2, 1]));

        Assert.Equal(3, file.NumberOfHMetrics);
        Assert.Equal(3, file.GlyphCount);
        Assert.Equal(12, file.Table("hmtx").Length);
        Assert.Equal(((ushort)500, (short)50), file.Metrics(0));
        Assert.Equal(((ushort)600, (short)-20), file.Metrics(1));
        Assert.Equal(((ushort)600, (short)30), file.Metrics(2));
    }

    [Fact]
    public void HeaderTablesKeepEverythingButTheirCounts()
    {
        var font = Load(new TestFontBuilder().IndexToLocFormat(0));

        var file = Read(FontSubsetter.Build(font, [0, 2]));

        var head = file.Table("head");
        AssertEqualExcept(Original(font, "head"), head, 8, 12, 50, 52);
        Assert.Equal([0x00, 0x01], head[50..52]);
        var hhea = file.Table("hhea");
        AssertEqualExcept(Original(font, "hhea"), hhea, 34, 36);
        Assert.Equal([0x00, 0x02], hhea[34..36]);
        var maxp = file.Table("maxp");
        AssertEqualExcept(Original(font, "maxp"), maxp, 4, 6);
        Assert.Equal([0x00, 0x02], maxp[4..6]);
    }

    [Fact]
    public void PostBecomesFormat3()
    {
        var post = Enumerable.Range(0, 42).Select(i => (byte)(i + 100)).ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(post, 0x00020000);
        BinaryPrimitives.WriteUInt32BigEndian(post.AsSpan(4), 0xFFF40000); // −12°

        var file = Read(FontSubsetter.Build(Load(new TestFontBuilder().Table("post", post)), [0, 2]));

        Assert.Equal([0x00, 0x03, 0x00, 0x00, .. post[4..32]], file.Table("post"));
    }

    [Fact]
    public void WithoutPostTheSubsetHasNone()
    {
        var file = Read(FontSubsetter.Build(Load(new TestFontBuilder().Without("post")), [0, 2]));

        Assert.Equal(["glyf", "head", "hhea", "hmtx", "loca", "maxp"], file.Tags);
    }

    [Fact]
    public void KeepsTheHintingTablesAndDropsTheRest()
    {
        var font = Load(WithExtraTables(new TestFontBuilder()));

        var file = Read(FontSubsetter.Build(font, [0, 2]));

        Assert.Equal(HintedTags, file.Tags);
        foreach (var tag in new[] { "cvt ", "fpgm", "prep", "gasp" })
        {
            Assert.Equal(Original(font, tag), file.Table(tag));
        }
    }

    [Fact]
    public void TheFileIsAValidSfnt()
    {
        var bytes = FontSubsetter.Build(Load(WithExtraTables(new TestFontBuilder())), [0, 2]).Data.ToArray();
        var file = new TestFontFile(bytes);

        Assert.Equal(0x00010000u, file.SfntVersion);
        Assert.Equal(((ushort)128, (ushort)3, (ushort)48), file.SearchFields);
        foreach (var tag in file.Tags)
        {
            var table = file.Table(tag);
            if (tag == "head")
            {
                Array.Clear(table, 8, 4);
            }

            Assert.Equal(FontFileAssembler.Checksum(table), file.RecordChecksum(tag));
        }

        Assert.Equal(0xB1B0AFBAu, FontFileAssembler.Checksum(bytes));
    }

    [Fact]
    public void TheSameInputGivesTheSameBytes()
    {
        var font = Load(new TestFontBuilder());
        var noto = File.ReadAllBytes(TestFonts.NotoSansPath("Regular"));

        Assert.Equal(FontSubsetter.Build(font, [0, 3, 2]).Data.ToArray(), FontSubsetter.Build(font, [0, 3, 2]).Data.ToArray());
        Assert.Equal(
            FontSubsetter.Build(TrueTypeFont.Load(noto), [0, 134, 3]).Data.ToArray(),
            FontSubsetter.Build(TrueTypeFont.Load(noto), [0, 134, 3]).Data.ToArray());
    }

    [Fact]
    public void RefusesAFontThatMayNotBeSubset()
    {
        var font = Load(new TestFontBuilder().FsType(0x0100));

        var error = Assert.Throws<InvalidOperationException>(() => FontSubsetter.Build(font, [0, 2]));

        Assert.Contains("may not be subset", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesASubsetTooLargeForOneFile()
    {
        var font = Load(new TestFontBuilder().RawGlyph(500, new byte[40_000]));
        ushort[] glyphs = [0, .. Enumerable.Repeat((ushort)4, 65_534)];

        var error = Assert.Throws<InvalidOperationException>(() => FontSubsetter.Build(font, glyphs));

        Assert.Contains("too large", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DamageMessagesNameTheFont()
    {
        var font = Load(new TestFontBuilder().CompositeGlyph(700, new TestComponent(4, 0x0002)));

        var error = Assert.Throws<InvalidDataException>(() => FontSubsetter.Build(font, [0, 4]));

        Assert.StartsWith("TestFont-Regular: ", error.Message, StringComparison.Ordinal);
        Assert.Contains("cycle", error.Message, StringComparison.Ordinal);
        Assert.IsType<InvalidDataException>(error.InnerException);
    }

    [Theory]
    [MemberData(nameof(TestFonts.Styles), MemberType = typeof(TestFonts))]
    public void NotoSansSubsetKeepsHintingAndItsAngle(string style)
    {
        var font = TestFonts.NotoSans(style);

        var file = Read(FontSubsetter.Build(font, [0, 134]));

        Assert.Equal(HintedTags, file.Tags);
        foreach (var tag in new[] { "cvt ", "fpgm", "prep", "gasp" })
        {
            Assert.Equal(Original(font, tag), file.Table(tag));
        }

        Assert.Equal([0x00, 0x03, 0x00, 0x00, .. Original(font, "post")[4..32]], file.Table("post"));
    }

    private static TestComponent[] C(ushort first, ushort second) => [new(first, 0x0002), new(second, 0x0002)];

    /// <summary>The hinting tables F-08 keeps and seven tables it drops, each with its own bytes.</summary>
    private static TestFontBuilder WithExtraTables(TestFontBuilder builder)
    {
        string[] tags = ["cvt ", "fpgm", "prep", "gasp", "kern", "GPOS", "GSUB", "GDEF", "hdmx", "LTSH", "VDMX"];
        for (var i = 0; i < tags.Length; i++)
        {
            builder.Table(tags[i], Enumerable.Range(0, 6 + i).Select(b => (byte)((16 * i) + b)).ToArray());
        }

        return builder;
    }

    /// <summary>
    /// Each subset glyph, with its component IDs written back to the original glyphs they stand for, has exactly the
    /// bytes of its original: only the IDs changed.
    /// </summary>
    private static void AssertSameGlyphsOnceMappedBack(TrueTypeFont font, FontSubset subset)
    {
        var file = Read(subset);
        for (var glyph = 0; glyph < subset.OriginalGlyphIds.Count; glyph++)
        {
            var data = file.Glyph(glyph);
            foreach (var component in CompositeGlyph.ReadComponents(data, file.GlyphCount))
            {
                BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(component.GlyphIdOffset), subset.OriginalGlyphIds[component.GlyphId]);
            }

            Assert.Equal(font.GetGlyphData(subset.OriginalGlyphIds[glyph]).ToArray(), data);
        }
    }

    /// <summary>Equal, except in the ranges given as start and end pairs.</summary>
    private static void AssertEqualExcept(byte[] expected, byte[] actual, params int[] ranges)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            var skipped = Enumerable.Range(0, ranges.Length / 2).Any(r => i >= ranges[2 * r] && i < ranges[(2 * r) + 1]);
            if (!skipped)
            {
                Assert.True(expected[i] == actual[i], $"Byte {i}: expected 0x{expected[i]:X2}, got 0x{actual[i]:X2}.");
            }
        }
    }

    private static byte[] Original(TrueTypeFont font, string tag)
    {
        Assert.True(font.TryGetTable(tag, out var table), $"The font has no '{tag}' table.");
        return table.ToArray();
    }

    private static TestFontFile Read(FontSubset subset) => new(subset.Data.ToArray());

    private static TrueTypeFont Load(TestFontBuilder builder) => TrueTypeFont.Load(builder.Build());
}
