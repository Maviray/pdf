namespace Maviray.Pdf.Fonts;

/// <summary>
/// Where each glyph's data lies in <c>glyf</c>, from <c>loca</c> (spec F-03). The offsets never decrease and stay inside
/// <c>glyf</c>, so a glyph's range can always be sliced.
/// </summary>
internal sealed class GlyphLocationTable
{
    private readonly int[] _offsets;

    private GlyphLocationTable(int[] offsets)
    {
        _offsets = offsets;
    }

    public static GlyphLocationTable Parse(ReadOnlySpan<byte> table, short indexToLocFormat, int glyphCount, int glyfLength)
    {
        var reader = new FontTableReader(table, "loca");
        var entry = indexToLocFormat == 0 ? 2 : 4;
        var needed = (long)entry * (glyphCount + 1);
        if (reader.Length < needed)
        {
            throw reader.Error($"is too short: {glyphCount + 1} entries need {needed} bytes, and it has {reader.Length}");
        }

        var offsets = new int[glyphCount + 1];
        for (var i = 0; i <= glyphCount; i++)
        {
            long offset = indexToLocFormat == 0 ? 2L * reader.UInt16(2 * i) : reader.UInt32(4 * i);
            if (offset > glyfLength)
            {
                throw reader.Error($"places glyph data at {offset}, past the end of the 'glyf' table ({glyfLength} bytes)");
            }

            if (i > 0 && offset < offsets[i - 1])
            {
                throw reader.Error($"runs backwards: entry {i} ({offset}) is before entry {i - 1} ({offsets[i - 1]})");
            }

            offsets[i] = (int)offset;
        }

        return new GlyphLocationTable(offsets);
    }

    public (int Offset, int Length) RangeOf(int glyph) => (_offsets[glyph], _offsets[glyph + 1] - _offsets[glyph]);
}
