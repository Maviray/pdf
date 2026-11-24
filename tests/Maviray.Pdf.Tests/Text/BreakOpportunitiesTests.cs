using Maviray.Pdf.Tests.Text.Unicode;
using Maviray.Pdf.Text;

namespace Maviray.Pdf.Tests.Text;

// A line never breaks inside a grapheme cluster: a break that UAX #14 allows there is dropped (spec T-05).
public sealed class BreakOpportunitiesTests
{
    [Fact]
    public void DropsBreaksInsideClusters()
    {
        // UAX #14 allows a break between a space and a mark after it (LB18), but the mark joins the space's cluster.
        var text = "a " + char.ConvertFromUtf32(0x0301) + "b";

        Assert.Equal(LineBreakKind.Allowed, LineBreaker.FindBreaks(text)[2]);
        Assert.Equal(LineBreakKind.None, BreakOpportunities.Find(text)[2]);
    }

    [Fact]
    public void KeepsBreaksBetweenClusters()
    {
        Assert.Equal(LineBreaker.FindBreaks("ab cd"), BreakOpportunities.Find("ab cd"));
    }

    [Fact]
    public void RequiredBreaksAreClusterStarts()
    {
        var misplaced = new List<string>();
        foreach (var (lineNumber, text, _, _, _) in LineBreakConformanceTests.ReadTests())
        {
            var breaks = LineBreaker.FindBreaks(text);
            var starts = GraphemeClusters.FindStarts(text);
            for (var i = 0; i < breaks.Length; i++)
            {
                if (breaks[i] == LineBreakKind.Required && !starts[i])
                {
                    misplaced.Add($"line {lineNumber}, position {i}");
                }
            }
        }

        Assert.Empty(misplaced.Take(20));
    }
}
