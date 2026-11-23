using Maviray.Pdf.Text;

namespace Maviray.Pdf.Tests.Text;

// Filling lines (spec T-08, T-09), at size 10 on the layout test font: a letter is 5 pt, a space 2.5 pt, W 9 pt,
// U+2010 3.2 pt, U+002D 3 pt, and a line 10 pt (0.8 em above the baseline, 0.2 em below).
public sealed class FillTests
{
    private static readonly string SoftHyphen = ((char)0x00AD).ToString();

    [Fact]
    public void GreedyFill()
    {
        AssertLines(Fill("aa bb cc", 25), ("aa bb ", 22.5), ("cc", 10));
        AssertLines(Fill("aa bb cc", 22.4999995), ("aa bb ", 22.5), ("cc", 10));
        AssertLines(Fill("aa bb cc", 22.49), ("aa ", 10), ("bb ", 10), ("cc", 10));
    }

    [Fact]
    public void HangingSpaces()
    {
        AssertLines(Fill("a   \nb", 100), ("a   \n", 5), ("b", 5));
        AssertLines(Fill("   ", 100), ("   ", 0));
    }

    [Fact]
    public void RequiredBreaks()
    {
        AssertLines(Fill("a\n\nb", 100), ("a\n", 5), ("\n", 0), ("b", 5));
        AssertLines(Fill("a\r\nb", 100), ("a\r\n", 5), ("b", 5));
        AssertLines(Fill("a\n", 100), ("a\n", 5));

        var newline = Fill("\n", 100);
        AssertLines(newline, ("\n", 0));
        Assert.Equal(10, newline.Lines[0].Height);

        var empty = Fill("", 100);
        Assert.Empty(empty.Lines);
        Assert.Equal(0, empty.Height);
        Assert.Equal(0, empty.Width);
    }

    [Fact]
    public void LinesPartitionTheText()
    {
        const string Text = "aaa bbb\nccc  ddd eee\r\nfff\n";
        var layout = Fill(Text, 30);

        Assert.Equal(Text, string.Concat(layout.Lines.Select(l => Text.Substring(l.Start, l.Length))));
    }

    [Fact]
    public void ASoftHyphenAtTheLineEndDrawsAHyphen()
    {
        AssertLines(Fill("aaa" + SoftHyphen + "bbb", 18.5), ("aaa" + SoftHyphen, 18.2), ("bbb", 15));

        var withoutU2010 = LayoutTestFont.Create(mapHyphen: false);
        AssertLines(Fill("aaa" + SoftHyphen + "bbb", 18.5, LayoutTestFont.Style(font: withoutU2010)), ("aaa" + SoftHyphen, 18), ("bbb", 15));
    }

    // What follows the soft hyphen (a no-break space, 2.5 pt) is narrower than the hyphen (3.2 pt), so the whole line
    // is narrower than the line ended at the soft hyphen. Filled again at its own width, it must stay one line.
    [Fact]
    public void ASoftHyphenFollowedByLessThanAHyphenKeepsItsLine()
    {
        var prepared = Prepare("aaa" + SoftHyphen + char.ConvertFromUtf32(0x00A0));
        var natural = prepared.Fill(double.PositiveInfinity);

        Assert.Equal(17.5, natural.Width, 9);
        AssertLines(prepared.Fill(natural.Width), ("aaa" + SoftHyphen + char.ConvertFromUtf32(0x00A0), 17.5));
    }

    [Fact]
    public void ASoftHyphenElsewhereAddsNothing()
    {
        AssertLines(Fill("aaa" + SoftHyphen, 100), ("aaa" + SoftHyphen, 15));
        AssertLines(Fill("aaa" + SoftHyphen + "\nbbb", 100), ("aaa" + SoftHyphen + "\n", 15), ("bbb", 15));
        AssertLines(Fill("aaa" + SoftHyphen + "bbb", 100), ("aaa" + SoftHyphen + "bbb", 30));
    }

    [Fact]
    public void AWordTooWideForItsLineBreaksBetweenClusters()
    {
        AssertLines(Fill("aaaaaa", 12), ("aa", 10), ("aa", 10), ("aa", 10));
        AssertLines(Fill("aaaaa bb", 12), ("aa", 10), ("aa", 10), ("a ", 5), ("bb", 10));

        // One cluster wider than the line: it takes the line on its own, and reports its true width.
        AssertLines(Fill("W", 1), ("W", 9));
        AssertLines(Fill("WW", 1), ("W", 9), ("W", 9));
    }

    // Every line of a word too wide for any line is a cut. If a cut looked at the rest of the word each time, this
    // would take about 2 * 10^10 steps.
    [Fact(Timeout = 10_000)]
    public async Task ALongWordAtATinyWidthStaysLinear()
    {
        const int Letters = 200_000;
        var prepared = Prepare(new string('a', Letters));

        var layout = await Task.Run(() => prepared.Fill(1), TestContext.Current.CancellationToken);

        Assert.Equal(Letters, layout.Lines.Count);
    }

    [Fact]
    public void LetterSpacingCountsBetweenClustersOnly()
    {
        var style = LayoutTestFont.Style(letterSpacing: 1);

        AssertLines(Fill("aa", 100, style), ("aa", 11));
        AssertLines(Fill("aa ", 100, style), ("aa ", 11));
    }

    [Fact]
    public void LineHeightsComeFromTheSpansOnTheLine()
    {
        var mixed = PreparedParagraph.Prepare(new Paragraph([
            new TextSpan("a", LayoutTestFont.Style(10)),
            new TextSpan("b", LayoutTestFont.Style(20)),
            new TextSpan("\nc", LayoutTestFont.Style(10)),
        ])).Fill(100);

        Assert.Equal(2, mixed.Lines.Count);
        Assert.Equal((16, 20, 0), (mixed.Lines[0].Baseline, mixed.Lines[0].Height, mixed.Lines[0].Top));
        Assert.Equal((8, 10, 20), (mixed.Lines[1].Baseline, mixed.Lines[1].Height, mixed.Lines[1].Top));
        Assert.Equal(30, mixed.Height);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    public void RefusesAWidthThatIsntPositive(double width)
    {
        var prepared = Prepare("a");

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => prepared.Fill(width));
        Assert.Equal("width", error.ParamName);
    }

    [Fact]
    public void AtInfinityEachPieceIsOneLine()
    {
        AssertLines(Fill("aa bb cc\ndd", double.PositiveInfinity), ("aa bb cc\n", 35), ("dd", 10));
        Assert.Equal(35, Fill("aa bb cc\ndd", double.PositiveInfinity).Width);
    }

    private static PreparedParagraph Prepare(string text, TextStyle? style = null) =>
        PreparedParagraph.Prepare(new Paragraph([new TextSpan(text, style ?? LayoutTestFont.Style())]));

    private static TextLayout Fill(string text, double width, TextStyle? style = null) => Prepare(text, style).Fill(width);

    private static void AssertLines(TextLayout layout, params (string Text, double Width)[] expected)
    {
        var text = layout.Source.Text;
        Assert.Equal(
            expected.Select(e => e.Text),
            layout.Lines.Select(l => text.Substring(l.Start, l.Length)));
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Width, layout.Lines[i].Width, 9);
        }
    }
}
