using Maviray.Pdf.Fonts;

namespace Maviray.Pdf.Tests.Fonts;

/// <summary>
/// Which glyphs a subset holds (spec F-08 step 1): the requested list, then every component it needs, in visit order.
/// Cycles, nesting deeper than 16 composites, and damaged glyphs are refused (F-05), and so is a subset of more than
/// 65,535 glyphs (F-07). The default test font has glyphs 0 to 3, with 2 and 3 simple; composites start at glyph 4.
/// </summary>
public sealed class GlyphClosureTests
{
    [Fact]
    public void SimpleGlyphsResolveToThemselves()
    {
        var font = Load(new TestFontBuilder());

        Assert.Equal((ushort[])[0, 3, 2, 3], GlyphClosure.Resolve(font, [0, 3, 2, 3]));
    }

    [Fact]
    public void ComponentsNotInTheListAreAppended()
    {
        var font = Load(new TestFontBuilder().CompositeGlyph(700, C(2, 3)));

        Assert.Equal((ushort[])[0, 4, 3, 2], GlyphClosure.Resolve(font, [0, 4, 3]));
    }

    [Fact]
    public void NestedComponentsAreAppendedInVisitOrder()
    {
        var font = Load(new TestFontBuilder().CompositeGlyph(700, C(2, 3)).CompositeGlyph(700, C(4, 2)));

        Assert.Equal((ushort[])[0, 5, 4, 2, 3], GlyphClosure.Resolve(font, [0, 5]));
    }

    [Fact]
    public void AComponentListedTwiceIsNotAppended()
    {
        var font = Load(new TestFontBuilder().CompositeGlyph(700, C(2, 3)));

        Assert.Equal((ushort[])[0, 4, 2, 2, 3], GlyphClosure.Resolve(font, [0, 4, 2, 2, 3]));
    }

    public static TheoryData<string> Cycles => new() { "a glyph naming itself", "two glyphs naming each other" };

    [Theory]
    [MemberData(nameof(Cycles))]
    public void RefusesACycle(string cycle)
    {
        var builder = new TestFontBuilder();
        if (cycle == "a glyph naming itself")
        {
            builder.CompositeGlyph(700, new TestComponent(4, 0x0002));
        }
        else
        {
            builder.CompositeGlyph(700, new TestComponent(5, 0x0002)).CompositeGlyph(700, new TestComponent(4, 0x0002));
        }

        var error = Assert.Throws<InvalidDataException>(() => GlyphClosure.Resolve(Load(builder), [0, 4]));

        Assert.Contains("'glyf'", error.Message, StringComparison.Ordinal);
        Assert.Contains("cycle", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NestingOf16IsAcceptedAnd17IsRefused()
    {
        var sixteen = GlyphClosure.Resolve(Load(Chain(16)), [0, 4]);
        var error = Assert.Throws<InvalidDataException>(() => GlyphClosure.Resolve(Load(Chain(17)), [0, 4]));

        Assert.Equal([0, .. Enumerable.Range(4, 16).Select(g => (ushort)g), 2], sixteen);
        Assert.Contains("deeper than 16", error.Message, StringComparison.Ordinal);
    }

    public static TheoryData<string> Damage => new() { "a component past the font", "data shorter than a header" };

    [Theory]
    [MemberData(nameof(Damage))]
    public void DamagedGlyphsInTheSubsetAreInvalid(string damage)
    {
        var builder = damage == "a component past the font"
            ? new TestFontBuilder().CompositeGlyph(700, C(2, 99))
            : new TestFontBuilder().RawGlyph(500, [0x00, 0x01, 0x00, 0x00]);

        var error = Assert.Throws<InvalidDataException>(() => GlyphClosure.Resolve(Load(builder), [0, 4]));

        Assert.Contains("'glyf'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BrokenGlyphsOutsideTheSubsetDoNotMatter()
    {
        var font = Load(new TestFontBuilder().CompositeGlyph(700, new TestComponent(4, 0x0002)));

        Assert.Equal((ushort[])[0, 2, 3], GlyphClosure.Resolve(font, [0, 2, 3]));
    }

    [Fact]
    public void ArgumentsAreChecked()
    {
        var font = Load(new TestFontBuilder());

        Assert.Throws<ArgumentNullException>(() => GlyphClosure.Resolve(font, null!));
        Assert.Throws<ArgumentException>(() => GlyphClosure.Resolve(font, []));
        Assert.Throws<ArgumentException>(() => GlyphClosure.Resolve(font, [2]));
        Assert.Throws<ArgumentOutOfRangeException>(() => GlyphClosure.Resolve(font, [0, 4]));
    }

    [Fact]
    public void MoreThan65535GlyphsAreRefused()
    {
        var font = Load(new TestFontBuilder().CompositeGlyph(700, C(2, 3)));
        ushort[] listedToTheLimit = [0, 2, 3, .. Enumerable.Repeat((ushort)4, 65_532)];
        ushort[] appendedToTheLimit = [0, .. Enumerable.Repeat((ushort)4, 65_532)]; // 2 and 3 appended make 65,535
        ushort[] appendedPastIt = [0, .. Enumerable.Repeat((ushort)4, 65_533)]; // 65,536 once 2 and 3 are appended
        ushort[] listedPastIt = [0, .. Enumerable.Repeat((ushort)2, 65_535)]; // 65,536 simple glyphs

        Assert.Equal(65_535, GlyphClosure.Resolve(font, listedToTheLimit).Length);
        Assert.Equal(65_535, GlyphClosure.Resolve(font, appendedToTheLimit).Length);
        foreach (var glyphs in new[] { appendedPastIt, listedPastIt })
        {
            var error = Assert.Throws<InvalidOperationException>(() => GlyphClosure.Resolve(font, glyphs));
            Assert.Contains("65,535", error.Message, StringComparison.Ordinal);
        }
    }

    // Read with a throwaway script while planning: "Ä" (134) is "A" (36) and a mark (106) in every style, both simple.
    [Theory]
    [MemberData(nameof(TestFonts.Styles), MemberType = typeof(TestFonts))]
    public void NotoSansADieresisBringsItsComponents(string style)
    {
        Assert.Equal((ushort[])[0, 134, 36, 106], GlyphClosure.Resolve(TestFonts.NotoSans(style), [0, 134]));
    }

    private static TestComponent[] C(ushort first, ushort second) => [new(first, 0x0002), new(second, 0x0002)];

    /// <summary>Glyphs 4 to 3 + <paramref name="length"/>, each the single component of the one before; the last uses glyph 2.</summary>
    private static TestFontBuilder Chain(int length)
    {
        var builder = new TestFontBuilder();
        for (var glyph = 4; glyph < 4 + length; glyph++)
        {
            builder.CompositeGlyph(700, new TestComponent(glyph == 3 + length ? (ushort)2 : (ushort)(glyph + 1), 0x0002));
        }

        return builder;
    }

    private static TrueTypeFont Load(TestFontBuilder builder) => TrueTypeFont.Load(builder.Build());
}
