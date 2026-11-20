namespace Maviray.Pdf.Fonts;

/// <summary>The values the library reads from <c>OS/2</c>, which is optional (spec F-03).</summary>
internal sealed record Os2Table(
    ushort Version,
    ushort WeightClass,
    ushort FsType,
    ushort FsSelection,
    LineMetrics Typographic,
    LineMetrics Windows,
    int? CapHeight)
{
    public static Os2Table Parse(ReadOnlySpan<byte> table)
    {
        var reader = new FontTableReader(table, "OS/2");
        var version = reader.UInt16(0);
        var needed = version switch
        {
            0 => 78,
            1 => 86,
            < 5 => 96,
            _ => 100,
        };
        if (reader.Length < needed)
        {
            throw reader.Error($"is version {version}, which needs {needed} bytes, and it has {reader.Length}");
        }

        return new Os2Table(
            version,
            reader.UInt16(4),
            reader.UInt16(8),
            reader.UInt16(62),
            new LineMetrics(reader.Int16(68), reader.Int16(70), reader.Int16(72)),
            new LineMetrics(reader.UInt16(74), -reader.UInt16(76), 0),
            version >= 2 ? reader.Int16(88) : null);
    }
}
