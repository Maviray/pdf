namespace Maviray.Pdf.Fonts;

/// <summary>The line metrics and <c>numberOfHMetrics</c> from <c>hhea</c> (spec F-03).</summary>
internal sealed record HorizontalHeaderTable(LineMetrics Metrics, int NumberOfHMetrics)
{
    private const int Length = 36;

    public static HorizontalHeaderTable Parse(ReadOnlySpan<byte> table, int glyphCount)
    {
        var reader = new FontTableReader(table, "hhea");
        if (reader.Length < Length)
        {
            throw reader.Error($"is too short: it needs {Length} bytes and has {reader.Length}");
        }

        var metrics = reader.UInt16(34);
        if (metrics == 0 || metrics > glyphCount)
        {
            throw reader.Error($"gives numberOfHMetrics = {metrics}, outside the range 1 to the glyph count {glyphCount}");
        }

        return new HorizontalHeaderTable(new LineMetrics(reader.Int16(4), reader.Int16(6), reader.Int16(8)), metrics);
    }
}
