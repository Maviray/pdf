using System.Globalization;

namespace Maviray.Pdf.Text;

/// <summary>
/// The extended grapheme clusters of a text, from .NET's <see cref="StringInfo"/> (spec T-05). It is fully managed and
/// works the same in invariant-globalization mode. Its one gap against Unicode 15.1 and later is rule GB9c (Indic
/// conjuncts), which v1's scripts don't meet.
/// </summary>
internal static class GraphemeClusters
{
    /// <summary>
    /// One entry per UTF-16 boundary of <paramref name="text"/>, <c>text.Length + 1</c> in all: true where a cluster
    /// starts, and at the end.
    /// </summary>
    public static bool[] FindStarts(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var starts = new bool[text.Length + 1];
        for (var i = 0; i < text.Length; i += StringInfo.GetNextTextElementLength(text, i))
        {
            starts[i] = true;
        }

        starts[text.Length] = true;
        return starts;
    }
}
