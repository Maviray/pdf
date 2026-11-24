using Maviray.Pdf.Tests.Fonts;
using Maviray.Pdf.Text;

namespace Maviray.Pdf.Tests.Text;

// Underline and strikethrough (spec T-11), at size 10 on the layout test font: a letter is 5 pt, a space 2.5 pt, and
// the baseline is 8 pt below the line's top. Its underline is -100 and 50, its strikeout 50 at 300.
public sealed class DecorationTests
{
    [Fact]
    public void AnUnderlineSpansTheRunWithoutHangingSpaces()
    {
        var line = Fill(("ab cd ", LayoutTestFont.Style(underline: true)));

        Assert.Equal([new TextDecoration(DecorationKind.Underline, 0, 0, 22.5, 9, 0.5)], line.Decorations);
    }

    [Fact]
    public void LetterSpacingBetweenTheRunsClustersCounts()
    {
        var line = Fill(("ab cd ", LayoutTestFont.Style(letterSpacing: 1, underline: true)));

        Assert.Equal(26.5, Assert.Single(line.Decorations).Width, 9);
    }

    [Fact]
    public void AStrikethroughUsesTheFontsStrikeout()
    {
        var line = Fill(("ab", LayoutTestFont.Style(strikethrough: true)));

        Assert.Equal([new TextDecoration(DecorationKind.Strikethrough, 0, 0, 10, 5, 0.5)], line.Decorations);
    }

    [Fact]
    public void WithoutAStrikeoutTheStrikethroughSitsAtThreeTenthsOfAnEm()
    {
        var font = LayoutTestFont.Create(b => b.Strikeout(0, 0).Underline(-100, 80));
        var line = Fill(("ab", LayoutTestFont.Style(strikethrough: true, font: font)));

        Assert.Equal([new TextDecoration(DecorationKind.Strikethrough, 0, 0, 10, 5, 0.8)], line.Decorations);
    }

    [Fact]
    public void WithoutAnUnderlineThicknessTheUnderlineFallsBack()
    {
        var font = LayoutTestFont.Create(b => b.Underline(-300, 0));
        var line = Fill(("ab", LayoutTestFont.Style(underline: true, font: font)));

        Assert.Equal([new TextDecoration(DecorationKind.Underline, 0, 0, 10, 9, 0.5)], line.Decorations);
    }

    [Fact]
    public void EachDecoratedSpanIsItsOwnRectangle()
    {
        var line = Fill(("ab", LayoutTestFont.Style(underline: true)), ("cd", LayoutTestFont.Style(underline: true)), ("ef", LayoutTestFont.Style()));

        Assert.Equal(
            [new TextDecoration(DecorationKind.Underline, 0, 0, 10, 9, 0.5), new TextDecoration(DecorationKind.Underline, 1, 10, 10, 9, 0.5)],
            line.Decorations);
    }

    // A cluster that crosses spans belongs to its first code point's span, so its decoration follows that span.
    [Fact]
    public void AClusterAcrossSpansIsDecoratedByItsFirstSpan()
    {
        var mark = char.ConvertFromUtf32(0x0301) + "x";

        var first = Fill(("e", LayoutTestFont.Style(letterSpacing: 1, underline: true)), (mark, LayoutTestFont.Style(letterSpacing: 3)));
        Assert.Equal([new TextDecoration(DecorationKind.Underline, 0, 0, 5, 9, 0.5)], first.Decorations);

        var second = Fill(("e", LayoutTestFont.Style(letterSpacing: 1)), (mark, LayoutTestFont.Style(letterSpacing: 3, underline: true)));
        Assert.Equal([new TextDecoration(DecorationKind.Underline, 1, 6, 5, 9, 0.5)], second.Decorations);
    }

    // The values the spec quotes (T-11): at 10 pt the underline spans 1 to 1.5 pt below the baseline, and the
    // strikethrough 3.22 to 2.72 pt above it. Noto Sans's baseline is 10.69 pt below the line's top.
    [Fact]
    public void NotoSansRegularMatchesTheSpec()
    {
        var style = new SpanStyle(TestFonts.NotoSans("Regular"), 10, underline: true, strikethrough: true);
        var line = Fill(("Ab", style));
        var underline = line.Decorations.Single(d => d.Kind == DecorationKind.Underline);
        var strikethrough = line.Decorations.Single(d => d.Kind == DecorationKind.Strikethrough);

        Assert.Equal(line.Baseline + 1, underline.Top, 9);
        Assert.Equal(line.Baseline + 1.5, underline.Top + underline.Thickness, 9);
        Assert.Equal(line.Baseline - 3.22, strikethrough.Top, 9);
        Assert.Equal(line.Baseline - 2.72, strikethrough.Top + strikethrough.Thickness, 9);
        Assert.Equal(10.69, line.Baseline, 9);
    }

    private static TextLine Fill(params (string Text, SpanStyle Style)[] spans) =>
        PreparedParagraph.Prepare(new Paragraph(spans.Select(s => new TextSpan(s.Text, s.Style)))).Fill(1000).Lines[0];
}
