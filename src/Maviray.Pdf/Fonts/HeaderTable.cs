namespace Maviray.Pdf.Fonts;

/// <summary>The values the library reads from <c>head</c> (spec F-03).</summary>
internal sealed record HeaderTable(int UnitsPerEm, GlyphBounds Bounds, ushort MacStyle, short IndexToLocFormat)
{
    private const int Length = 54;

    public static HeaderTable Parse(ReadOnlySpan<byte> table)
    {
        var reader = new FontTableReader(table, "head");
        if (reader.Length < Length)
        {
            throw reader.Error($"is too short: it needs {Length} bytes and has {reader.Length}");
        }

        var unitsPerEm = reader.UInt16(18);
        if (unitsPerEm is < 16 or > 16384)
        {
            throw reader.Error($"gives unitsPerEm = {unitsPerEm}, outside the range 16 to 16384");
        }

        var format = reader.Int16(50);
        if (format is not (0 or 1))
        {
            throw reader.Error($"gives indexToLocFormat = {format}, which isn't 0 or 1");
        }

        var bounds = new GlyphBounds(reader.Int16(36), reader.Int16(38), reader.Int16(40), reader.Int16(42));
        return new HeaderTable(unitsPerEm, bounds, reader.UInt16(44), format);
    }
}
