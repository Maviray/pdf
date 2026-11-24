namespace Maviray.Pdf.Text;

/// <summary>
/// Where a line may break (spec T-05 and T-06): the opportunities of UAX #14, except that a line never breaks inside a
/// grapheme cluster. A required break always falls between clusters, because every break character is a control in
/// UAX #29.
/// </summary>
internal static class BreakOpportunities
{
    /// <summary><see cref="LineBreaker.FindBreaks"/>, with each break allowed inside a cluster made none.</summary>
    public static LineBreakKind[] Find(string text)
    {
        var breaks = LineBreaker.FindBreaks(text);
        var starts = GraphemeClusters.FindStarts(text);
        for (var i = 0; i < breaks.Length; i++)
        {
            if (breaks[i] == LineBreakKind.Allowed && !starts[i])
            {
                breaks[i] = LineBreakKind.None;
            }
        }

        return breaks;
    }
}
