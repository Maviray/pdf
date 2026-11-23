namespace Maviray.Pdf.Fonts;

/// <summary>
/// The advance width and left side bearing of every glyph, from <c>hmtx</c> (spec F-03, F-08). Glyphs past
/// <c>numberOfHMetrics</c> take the last advance listed, and their side bearings follow the metrics.
/// </summary>
internal sealed class HorizontalMetricsTable
{
    private readonly ushort[] _advances;
    private readonly short[] _sideBearings;

    private HorizontalMetricsTable(ushort[] advances, short[] sideBearings)
    {
        _advances = advances;
        _sideBearings = sideBearings;
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
        var sideBearings = new short[glyphCount];
        for (var glyph = 0; glyph < numberOfHMetrics; glyph++)
        {
            advances[glyph] = reader.UInt16(4 * glyph);
            sideBearings[glyph] = reader.Int16((4 * glyph) + 2);
        }

        for (var glyph = numberOfHMetrics; glyph < glyphCount; glyph++)
        {
            sideBearings[glyph] = reader.Int16((4 * numberOfHMetrics) + (2 * (glyph - numberOfHMetrics)));
        }

        Array.Fill(advances, advances[numberOfHMetrics - 1], numberOfHMetrics, glyphCount - numberOfHMetrics);
        return new HorizontalMetricsTable(advances, sideBearings);
    }

    public int AdvanceOf(int glyph) => _advances[glyph];

    public int LeftSideBearingOf(int glyph) => _sideBearings[glyph];
}
