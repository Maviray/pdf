using Maviray.Pdf.Text;

namespace Maviray.Pdf.Tests.Text;

// A fill can start at the start of a line of an earlier fill (T-14, amended in P1.11), so a paragraph split across
// pages fills what is left of it at each page part's own width.
public sealed class FillFromTests
{
    [Fact]
    public void AFillFromALineStartGivesTheRestOfTheLines()
    {
        var paragraph = Prepare("aaa bbb ccc ddd eee fff");
        var whole = paragraph.Fill(40);

        var rest = paragraph.Fill(40, whole.Lines[2].Start);

        Assert.Equal(whole.Lines.Skip(2).Select(line => (line.Start, line.Length, line.Width)), rest.Lines.Select(line => (line.Start, line.Length, line.Width)));
        Assert.Equal(0, rest.Lines[0].Top);
    }

    [Fact]
    public void AFillFromALineStartWrapsTheRestAtItsOwnWidth()
    {
        var paragraph = Prepare("aaa bbb ccc ddd eee fff");
        var from = paragraph.Fill(40).Lines[1].Start;

        var rest = paragraph.Fill(1000, from);

        Assert.Equal((from, paragraph.Text.Length - from), (Assert.Single(rest.Lines).Start, rest.Lines[0].Length));
    }

    [Fact]
    public void AFillFromTheEndHasNoLines() => Assert.Empty(Prepare("aaa bbb").Fill(40, 7).Lines);

    // An x with an acute has no composed form, so NFC leaves the mark after it, in one cluster.
    [Fact]
    public void AFillFromInsideAClusterThrows()
    {
        var paragraph = Prepare("x\u0301y");

        Assert.Throws<ArgumentOutOfRangeException>(() => paragraph.Fill(40, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => paragraph.Fill(40, 4));
    }

    private static PreparedParagraph Prepare(string text) =>
        PreparedParagraph.Prepare(new Paragraph([new TextSpan(text, new SpanStyle(LayoutTestFont.Font, 10))]));
}
