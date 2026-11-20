namespace Maviray.Pdf.Fonts;

/// <summary>
/// The advance width of every glyph, from <c>hmtx</c> (spec F-03). Glyphs past <c>numberOfHMetrics</c> take the last
/// advance listed.
/// </summary>
internal sealed class HorizontalMetricsTable
{
    private readonly ushort[] _advances;

    private HorizontalMetricsTable(ushort[] advances)
    {
        _advances = advances;
    }

    public static HorizontalMetricsTable Parse(ReadOnlySpan<byte> table, int numberOfHMetrics, int glyphCount)
    {
        var reader = new FontTableReader(table, "hmtx");
        var needed = (4L * numberOfHMetrics) + (2L * (glyphCount - numberOfHMetrics));
        if (reader.Length < needed)
        {
            throw reader.Error(
                $"is too short: {numberOfHMetrics} metrics and {glyphCount - numberOfHMetrics} side bearings need {needed} bytes, and it has {reader.Length}");
        }

        var advances = new ushort[glyphCount];
        for (var glyph = 0; glyph < numberOfHMetrics; glyph++)
        {
            advances[glyph] = reader.UInt16(4 * glyph);
        }

        Array.Fill(advances, advances[numberOfHMetrics - 1], numberOfHMetrics, glyphCount - numberOfHMetrics);
        return new HorizontalMetricsTable(advances);
    }

    public int AdvanceOf(int glyph) => _advances[glyph];
}
