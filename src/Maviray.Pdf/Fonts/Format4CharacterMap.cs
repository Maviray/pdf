using System.Buffers.Binary;

namespace Maviray.Pdf.Fonts;

/// <summary>
/// A format 4 <c>cmap</c> subtable: segments of the Basic Multilingual Plane, each mapped by a delta or through the glyph
/// array (spec F-04). Parsing checks that the end codes rise, that the last segment ends at <c>0xFFFF</c>, and that every
/// glyph array position a segment can reach lies inside the subtable.
/// </summary>
internal sealed class Format4CharacterMap : CharacterMap
{
    private readonly ushort[] _ends;
    private readonly ushort[] _starts;
    private readonly ushort[] _deltas;
    private readonly int[] _rangePositions; // byte position of the start code's glyph array entry, or -1
    private readonly byte[] _data;
    private readonly int _glyphCount;

    private Format4CharacterMap(ushort[] ends, ushort[] starts, ushort[] deltas, int[] rangePositions, byte[] data, int glyphCount)
    {
        _ends = ends;
        _starts = starts;
        _deltas = deltas;
        _rangePositions = rangePositions;
        _data = data;
        _glyphCount = glyphCount;
    }

    /// <param name="subtable">The bytes from the subtable's offset to the end of the <c>cmap</c> table.</param>
    /// <param name="glyphCount">The font's glyph count: a mapping to a glyph at or past it counts as unmapped.</param>
    public static Format4CharacterMap ParseSubtableBytes(ReadOnlySpan<byte> subtable, int glyphCount)
    {
        var reader = new FontTableReader(subtable, "cmap");
        var doubled = reader.UInt16(6);
        if (doubled == 0 || doubled % 2 != 0)
        {
            throw reader.Error($"has a format 4 subtable with segCountX2 = {doubled}, which isn't a positive even number");
        }

        var count = doubled / 2;
        var ends = new ushort[count];
        var starts = new ushort[count];
        var deltas = new ushort[count];
        var rangePositions = new int[count];
        var startCodes = 16 + doubled;
        var idDeltas = 16 + (2 * doubled);
        var idRangeOffsets = 16 + (3 * doubled);
        var needed = 16 + (4 * doubled);
        for (var i = 0; i < count; i++)
        {
            ends[i] = reader.UInt16(14 + (2 * i));
            starts[i] = reader.UInt16(startCodes + (2 * i));
            deltas[i] = reader.UInt16(idDeltas + (2 * i));
            if (starts[i] > ends[i])
            {
                throw reader.Error($"has a format 4 segment that starts at 0x{starts[i]:X4}, after its end 0x{ends[i]:X4}");
            }

            if (i > 0 && ends[i] <= ends[i - 1])
            {
                throw reader.Error($"has format 4 segments out of order: 0x{ends[i]:X4} follows 0x{ends[i - 1]:X4}");
            }

            var rangeOffset = reader.UInt16(idRangeOffsets + (2 * i));
            if (rangeOffset == 0)
            {
                rangePositions[i] = -1;
                continue;
            }

            // The glyph of code c sits at &idRangeOffset[i] + idRangeOffset[i] + 2 × (c − startCode[i]).
            var first = idRangeOffsets + (2 * i) + rangeOffset;
            var lastEnd = (long)first + (2L * (ends[i] - starts[i])) + 2;
            if (lastEnd > reader.Length)
            {
                throw reader.Error(
                    $"has a format 4 segment 0x{starts[i]:X4}–0x{ends[i]:X4} whose glyph array runs to byte {lastEnd}, past the subtable's {reader.Length}");
            }

            rangePositions[i] = first;
            needed = Math.Max(needed, (int)lastEnd);
        }

        if (ends[count - 1] != 0xFFFF)
        {
            throw reader.Error("has a format 4 subtable that doesn't end with the 0xFFFF segment");
        }

        return new Format4CharacterMap(ends, starts, deltas, rangePositions, reader.Slice(0, needed).ToArray(), glyphCount);
    }

    public override IEnumerable<(int CodePoint, ushort GlyphId)> Mappings()
    {
        for (var i = 0; i < _ends.Length; i++)
        {
            for (int codePoint = _starts[i]; codePoint <= _ends[i]; codePoint++)
            {
                if (codePoint is >= 0xD800 and <= 0xDFFF)
                {
                    continue;
                }

                var glyph = GlyphOf(i, codePoint);
                if (glyph != 0)
                {
                    yield return (codePoint, glyph);
                }
            }
        }
    }

    protected override ushort Lookup(int codePoint)
    {
        if (codePoint > 0xFFFF)
        {
            return 0;
        }

        // The first segment whose end code is at or above the code point.
        int low = 0, high = _ends.Length - 1;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (_ends[middle] < codePoint)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return codePoint < _starts[low] || codePoint > _ends[low] ? (ushort)0 : GlyphOf(low, codePoint);
    }

    private ushort GlyphOf(int segment, int codePoint)
    {
        int glyph;
        if (_rangePositions[segment] < 0)
        {
            glyph = (codePoint + _deltas[segment]) & 0xFFFF;
        }
        else
        {
            var position = _rangePositions[segment] + (2 * (codePoint - _starts[segment]));
            glyph = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(position));
            if (glyph != 0)
            {
                glyph = (glyph + _deltas[segment]) & 0xFFFF;
            }
        }

        return glyph < _glyphCount ? (ushort)glyph : (ushort)0;
    }
}
