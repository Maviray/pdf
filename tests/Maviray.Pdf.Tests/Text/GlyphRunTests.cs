using Maviray.Pdf.Text;

namespace Maviray.Pdf.Tests.Text;

// Alignment, letter spacing and glyph runs (spec T-10), at size 10 on the layout test font: a letter is 5 pt.
public sealed class GlyphRunTests
{
    [Theory]
    [InlineData("Left", 0)]
    [InlineData("Center", 10)]
    [InlineData("Right", 20)]
    public void OffsetsFollowTheAlignment(string alignment, double offset)
    {
        var line = Fill([("aa", LayoutTestFont.Style())], 30, Enum.Parse<TextAlignment>(alignment)).Lines[0];

        Assert.Equal(offset, line.Offset);
        Assert.Equal(offset, line.Runs[0].X);
        Assert.Equal(offset, line.Runs[0].Glyphs[0].X);
    }

    [Fact]
    public void OverflowingLinesAndInfiniteWidthsStartAtZero()
    {
        Assert.Equal(0, Fill([("W", LayoutTestFont.Style())], 1, TextAlignment.Right).Lines[0].Offset);
        Assert.Equal(0, Fill([("aa", LayoutTestFont.Style())], double.PositiveInfinity, TextAlignment.Center).Lines[0].Offset);
    }

    [Fact]
    public void LetterSpacingFollowsEachClusterButTheLast()
    {
        var glyphs = Glyphs(Fill([("ab", LayoutTestFont.Style(letterSpacing: 1))], 100));

        Assert.Equal([new PositionedGlyph(LayoutTestFont.Letter, 'a', 0, 1), new PositionedGlyph(LayoutTestFont.Letter, 'b', 6, 0)], glyphs);
    }

    [Fact]
    public void MarksKeepNoSpacingInsideTheirCluster()
    {
        var text = "e" + char.ConvertFromUtf32(0x0301) + "b";
        var glyphs = Glyphs(Fill([(text, LayoutTestFont.Style(letterSpacing: 1))], 100));

        Assert.Equal(
            [
                new PositionedGlyph(LayoutTestFont.Letter, 'e', 0, 0),
                new PositionedGlyph(LayoutTestFont.CombiningAcute, 0x0301, 5, 1),
                new PositionedGlyph(LayoutTestFont.Letter, 'b', 6, 0),
            ],
            glyphs);
    }

    [Fact]
    public void EachSpanSegmentIsARun()
    {
        var line = Fill([("a", LayoutTestFont.Style()), ("b", LayoutTestFont.Style(20))], 100).Lines[0];

        Assert.Equal([0, 1], line.Runs.Select(r => r.SpanIndex));
        Assert.Equal([0.0, 5.0], line.Runs.Select(r => r.X));
    }

    [Fact]
    public void AClusterAcrossSpansTakesTheSpacingOfItsFirstSpan()
    {
        var line = Fill([("e", LayoutTestFont.Style(letterSpacing: 1)), (char.ConvertFromUtf32(0x0301) + "x", LayoutTestFont.Style(letterSpacing: 3))], 100).Lines[0];

        Assert.Equal([0, 1], line.Runs.Select(r => r.SpanIndex));
        Assert.Equal([new PositionedGlyph(LayoutTestFont.Letter, 'e', 0, 0)], line.Runs[0].Glyphs);
        Assert.Equal(
            [new PositionedGlyph(LayoutTestFont.CombiningAcute, 0x0301, 5, 1), new PositionedGlyph(LayoutTestFont.Letter, 'x', 6, 0)],
            line.Runs[1].Glyphs);
    }

    [Fact]
    public void ASoftHyphenAtTheLineEndDrawsAHyphenThatCopiesOutAsItself()
    {
        var layout = Fill([("aaa" + (char)0x00AD + "bbb", LayoutTestFont.Style())], 18.5);

        Assert.Equal(new PositionedGlyph(LayoutTestFont.Hyphen, 0x00AD, 15, 0), layout.Lines[0].Runs[0].Glyphs[^1]);
        Assert.Equal(4, layout.Lines[0].Runs[0].Glyphs.Count);
    }

    [Fact]
    public void HangingSpacesArentDrawnButTabsInsideAre()
    {
        Assert.Equal(2, Glyphs(Fill([("ab  ", LayoutTestFont.Style())], 100)).Count);

        var tab = Glyphs(Fill([("a\tb", LayoutTestFont.Style())], 100));
        Assert.Equal(new PositionedGlyph(LayoutTestFont.Space, 0x09, 5, 0), tab[1]);
    }

    private static TextLayout Fill((string Text, SpanStyle Style)[] spans, double width, TextAlignment alignment = TextAlignment.Left) =>
        PreparedParagraph.Prepare(new Paragraph(spans.Select(s => new TextSpan(s.Text, s.Style)), alignment)).Fill(width);

    private static List<PositionedGlyph> Glyphs(TextLayout layout) => [.. layout.Lines[0].Runs.SelectMany(r => r.Glyphs)];
}
