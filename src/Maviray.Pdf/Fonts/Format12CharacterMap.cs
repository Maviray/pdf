namespace Maviray.Pdf.Fonts;

/// <summary>
/// A format 12 <c>cmap</c> subtable: groups of consecutive code points mapped to consecutive glyphs (spec F-04). Parsing
/// checks that the groups are sorted and don't overlap.
/// </summary>
internal sealed class Format12CharacterMap : CharacterMap
{
    private readonly uint[] _starts;
    private readonly uint[] _ends;
    private readonly uint[] _startGlyphs;
    private readonly int _glyphCount;

    private Format12CharacterMap(uint[] starts, uint[] ends, uint[] startGlyphs, int glyphCount)
    {
        _starts = starts;
        _ends = ends;
        _startGlyphs = startGlyphs;
        _glyphCount = glyphCount;
    }

    /// <param name="subtable">The bytes from the subtable's offset to the end of the <c>cmap</c> table.</param>
    /// <param name="glyphCount">The font's glyph count: a mapping to a glyph at or past it counts as unmapped.</param>
    public static Format12CharacterMap ParseSubtableBytes(ReadOnlySpan<byte> subtable, int glyphCount)
    {
        var reader = new FontTableReader(subtable, "cmap");
        var count = reader.UInt32(12);
        var needed = 16 + (12L * count);
        if (needed > reader.Length)
        {
            throw reader.Error($"has a format 12 subtable of {count} groups, which need {needed} bytes; {reader.Length} are left");
        }

        var starts = new uint[count];
        var ends = new uint[count];
        var startGlyphs = new uint[count];
        for (var i = 0; i < count; i++)
        {
            var group = 16 + (12 * i);
            starts[i] = reader.UInt32(group);
            ends[i] = reader.UInt32(group + 4);
            startGlyphs[i] = reader.UInt32(group + 8);
            if (starts[i] > ends[i])
            {
                throw reader.Error($"has a format 12 group that starts at 0x{starts[i]:X}, after its end 0x{ends[i]:X}");
            }

            if (i > 0 && starts[i] < starts[i - 1])
            {
                throw reader.Error($"has format 12 groups out of order: 0x{starts[i]:X} follows 0x{starts[i - 1]:X}");
            }

            if (i > 0 && starts[i] <= ends[i - 1])
            {
                throw reader.Error($"has overlapping format 12 groups: 0x{starts[i]:X} is inside the group ending at 0x{ends[i - 1]:X}");
            }
        }

        return new Format12CharacterMap(starts, ends, startGlyphs, glyphCount);
    }

    public override IEnumerable<(int CodePoint, ushort GlyphId)> Mappings()
    {
        for (var i = 0; i < _starts.Length; i++)
        {
            var last = Math.Min(_ends[i], 0x10FFFFu);
            for (var codePoint = _starts[i]; codePoint <= last; codePoint++)
            {
                if (codePoint is >= 0xD800 and <= 0xDFFF)
                {
                    continue;
                }

                var glyph = GlyphOf(i, codePoint);
                if (glyph == 0 && _startGlyphs[i] + (ulong)(codePoint - _starts[i]) >= (ulong)_glyphCount)
                {
                    break; // the glyphs only rise from here, so the rest of the group is past the font too
                }

                if (glyph != 0)
                {
                    yield return ((int)codePoint, glyph);
                }
            }
        }
    }

    protected override ushort Lookup(int codePoint)
    {
        var value = (uint)codePoint;
        int low = 0, high = _starts.Length - 1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            if (value < _starts[middle])
            {
                high = middle - 1;
            }
            else if (value > _ends[middle])
            {
                low = middle + 1;
            }
            else
            {
                return GlyphOf(middle, value);
            }
        }

        return 0;
    }

    private ushort GlyphOf(int group, uint codePoint)
    {
        var glyph = _startGlyphs[group] + (ulong)(codePoint - _starts[group]);
        return glyph < (ulong)_glyphCount ? (ushort)glyph : (ushort)0;
    }
}
