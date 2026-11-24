using Maviray.Pdf.Fonts;
using Maviray.Pdf.Tests.Fonts;
using Maviray.Pdf.Text;

namespace Maviray.Pdf.Tests.Text;

// The text gate's greedy check (spec T-15) must not pass vacuously, so it is fed layouts that are wrong in each way it
// looks for. Every wrong layout is a real fill, made at another width or alignment, so its widths are consistent and
// only the property under test is off.
public sealed class GreedyLineCheckTests
{
    private static readonly TrueTypeFont Regular = TestFonts.NotoSans("Regular");
    private static readonly TrueTypeFont Bold = TestFonts.NotoSans("Bold");
    private static readonly TrueTypeFont Italic = TestFonts.NotoSans("Italic");

    [Fact]
    public void CorrectFillsHaveNoProblems()
    {
        var problems = new List<string>();
        foreach (var alignment in new[] { TextAlignment.Left, TextAlignment.Center, TextAlignment.Right })
        {
            foreach (var spans in new[] { ProseSpans(), TextGateText.MixedSpans(Regular, Bold, Italic) })
            {
                var prepared = PreparedParagraph.Prepare(new Paragraph(spans, alignment));
                foreach (var width in new[] { 60, 100, 160, 220, double.PositiveInfinity })
                {
                    problems.AddRange(GreedyLineCheck.Problems(prepared.Fill(width), width).Select(problem => $"{alignment} at {width}: {problem}"));
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void ALineThatCouldHoldTheNextSegmentIsReported()
    {
        var problems = GreedyLineCheck.Problems(Prose().Fill(200), 220);

        Assert.Contains(problems, problem => problem.Contains("could hold the next segment", StringComparison.Ordinal));
    }

    [Fact]
    public void ALineThatDoesntFitIsReported()
    {
        var problems = GreedyLineCheck.Problems(Prose().Fill(240), 220);

        Assert.Contains(problems, problem => problem.Contains("doesn't fit", StringComparison.Ordinal));
    }

    [Fact]
    public void ACutWhereASegmentFitsIsReported()
    {
        var prepared = PreparedParagraph.Prepare(new Paragraph([new TextSpan("Rechnungsnummer an", Style())]));

        var problems = GreedyLineCheck.Problems(prepared.Fill(40), 200);

        Assert.Contains(problems, problem => problem.StartsWith("Line 0 ", StringComparison.Ordinal) && problem.Contains("is cut", StringComparison.Ordinal));
    }

    [Fact]
    public void AnOffsetThatDoesntMatchTheAlignmentIsReported()
    {
        var right = PreparedParagraph.Prepare(new Paragraph(ProseSpans(), TextAlignment.Right)).Fill(160);
        var left = Prose();
        var mislabelled = new TextLayout(left, right.Width, right.Height, right.Lines);

        var problems = GreedyLineCheck.Problems(mislabelled, 160);

        var shifted = right.Lines.Count(line => line.Offset > 1e-9);
        Assert.True(shifted > 0, "No right-aligned line is shifted.");
        Assert.Equal(shifted, problems.Count(problem => problem.Contains("offset", StringComparison.Ordinal)));
    }

    private static TextStyle Style() => new(Regular, 10, TextGateText.LineHeight);

    private static TextSpan[] ProseSpans() => [new TextSpan(TextGateText.Prose, Style())];

    private static PreparedParagraph Prose() => PreparedParagraph.Prepare(new Paragraph(ProseSpans()));
}
