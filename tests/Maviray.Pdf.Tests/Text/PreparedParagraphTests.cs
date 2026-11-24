using Maviray.Pdf.Text;

namespace Maviray.Pdf.Tests.Text;

// Preparing a paragraph (spec T-03, T-05, T-07): each span cleaned and normalized on its own, the texts joined, and
// every code point resolved to glyphs by T-07's rules in order. The test font's numbers are in LayoutTestFont.
public sealed class PreparedParagraphTests
{
    [Fact]
    public void AMappedCodePointDrawsItsGlyph()
    {
        var prepared = Prepare(("ab", 10), ("c", 20));

        Assert.Equal([new PreparedGlyph(LayoutTestFont.Letter, 'a', 5)], prepared.GlyphsAt(0));
        Assert.Equal([new PreparedGlyph(LayoutTestFont.Letter, 'c', 10)], prepared.GlyphsAt(2));
    }

    [Fact]
    public void ATabDrawsASpaceAndCopiesOutAsATab()
    {
        var prepared = Prepare(("a\tb", 10));

        Assert.Equal([new PreparedGlyph(LayoutTestFont.Space, 0x09, 2.5)], prepared.GlyphsAt(1));
    }

    [Theory]
    [InlineData(0x00A0)]
    [InlineData(0x202F)]
    public void AnUnmappedSpaceSeparatorDrawsASpace(int codePoint)
    {
        var prepared = Prepare(("a" + char.ConvertFromUtf32(codePoint), 10));

        Assert.Equal([new PreparedGlyph(LayoutTestFont.Space, codePoint, 2.5)], prepared.GlyphsAt(1));
        Assert.Empty(prepared.Missing);
    }

    [Theory]
    [InlineData(0x200B)]
    [InlineData(0xFEFF)]
    [InlineData(0x0001)]
    public void AnUnmappedIgnorableOrControlDrawsNothing(int codePoint)
    {
        var prepared = Prepare(("a" + char.ConvertFromUtf32(codePoint), 10));

        Assert.Empty(prepared.GlyphsAt(1));
        Assert.Empty(prepared.Missing);
    }

    // An invisible character draws nothing even when the font maps it: Noto Sans maps U+2060 to a 6 pt gap, and
    // U+034F, U+200E and U+200F to glyphs with contours.
    [Theory]
    [InlineData(0x2060)]
    [InlineData(0x034F)]
    [InlineData(0x200E)]
    [InlineData(0x200F)]
    [InlineData(0x200B)]
    [InlineData(0xFEFF)]
    public void DefaultIgnorablesDrawNothingEvenWhenTheFontMapsThem(int codePoint)
    {
        var noto = PreparedParagraph.Prepare(new Paragraph([
            new TextSpan("a" + char.ConvertFromUtf32(codePoint), new SpanStyle(Fonts.TestFonts.NotoSans("Regular"), 10)),
        ]));
        var mapped = PreparedParagraph.Prepare(new Paragraph([
            new TextSpan("a" + char.ConvertFromUtf32(codePoint), LayoutTestFont.Style(font: LayoutTestFont.Create(b => b.Map(codePoint, LayoutTestFont.Letter)))),
        ]));

        Assert.Empty(noto.GlyphsAt(1));
        Assert.Empty(mapped.GlyphsAt(1));
    }

    [Fact]
    public void AnUnmappedCodePointDrawsItsDecomposition()
    {
        var font = LayoutTestFont.Create(b => b.Map(0x030A, LayoutTestFont.CombiningAcute));
        var prepared = PreparedParagraph.Prepare(new Paragraph([new TextSpan(char.ConvertFromUtf32(0x00C5), LayoutTestFont.Style(font: font))]));

        Assert.Equal(
            [new PreparedGlyph(LayoutTestFont.Letter, 0x41, 5), new PreparedGlyph(LayoutTestFont.CombiningAcute, 0x030A, 0)],
            prepared.GlyphsAt(0));
        Assert.Empty(prepared.Missing);
    }

    [Fact]
    public void AnythingElseDrawsNotdefAndIsReportedOncePerSpan()
    {
        var ideograph = char.ConvertFromUtf32(0x4E00);
        var prepared = Prepare((ideograph + ideograph, 10), (ideograph, 10));

        Assert.Equal([new PreparedGlyph(0, 0x4E00, 5)], prepared.GlyphsAt(0));
        Assert.Equal([new MissingGlyph(0x4E00, 0), new MissingGlyph(0x4E00, 1)], prepared.Missing);
    }

    [Fact]
    public void BreakCharactersAndSoftHyphensDrawNothingWhenPrepared()
    {
        var prepared = Prepare(("a\nb" + (char)0x00AD + "c", 10));

        Assert.Empty(prepared.GlyphsAt(1));
        Assert.Empty(prepared.GlyphsAt(3));
    }

    [Fact]
    public void EachSpanIsNormalizedOnItsOwn()
    {
        var diaeresis = char.ConvertFromUtf32(0x0308);

        Assert.Equal("K" + char.ConvertFromUtf32(0x00F6) + "ln", Prepare(("Ko" + diaeresis + "ln", 10)).Text);
        Assert.Equal("Ko" + diaeresis + "ln", Prepare(("Ko", 10), (diaeresis + "ln", 10)).Text);
    }

    [Fact]
    public void AClusterCanCrossASpanBoundary()
    {
        var prepared = Prepare(("e", 10), (char.ConvertFromUtf32(0x0301) + "x", 10));

        Assert.Equal([0, 1, 3], prepared.SpanStarts);
        Assert.True(prepared.IsClusterStart(0));
        Assert.False(prepared.IsClusterStart(1));
        Assert.True(prepared.IsClusterStart(2));
    }

    [Fact]
    public void EmptySpansAndNoSpans()
    {
        var empty = Prepare(("", 10), ("", 10));
        var none = PreparedParagraph.Prepare(new Paragraph([]));

        Assert.Equal("", empty.Text);
        Assert.Equal([0, 0, 0], empty.SpanStarts);
        Assert.Equal("", none.Text);
        Assert.Equal([0], none.SpanStarts);
    }

    [Fact]
    public void UnpairedSurrogatesBecomeTheReplacementCharacter()
    {
        var prepared = Prepare(("a" + (char)0xD800, 10));

        Assert.Equal("a" + char.ConvertFromUtf32(0xFFFD), prepared.Text);
        Assert.Equal([new MissingGlyph(0xFFFD, 0)], prepared.Missing);
    }

    private static PreparedParagraph Prepare(params (string Text, double Size)[] spans) =>
        PreparedParagraph.Prepare(new Paragraph(spans.Select(s => new TextSpan(s.Text, LayoutTestFont.Style(s.Size)))));
}
