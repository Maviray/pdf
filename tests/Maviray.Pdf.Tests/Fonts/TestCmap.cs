using System.Buffers.Binary;

namespace Maviray.Pdf.Tests.Fonts;

/// <summary>Builds <c>cmap</c> tables and subtables byte by byte, following the OpenType specification.</summary>
internal static class TestCmap
{
    /// <summary>A whole <c>cmap</c> table with one encoding record per subtable, in the order given.</summary>
    public static byte[] Table(params (ushort Platform, ushort Encoding, byte[] Subtable)[] subtables)
    {
        var bytes = new List<byte>();
        Put16(bytes, 0);
        Put16(bytes, (ushort)subtables.Length);
        var offset = 4 + (8 * subtables.Length);
        foreach (var (platform, encoding, subtable) in subtables)
        {
            Put16(bytes, platform);
            Put16(bytes, encoding);
            Put32(bytes, (uint)offset);
            offset += subtable.Length;
        }

        foreach (var (_, _, subtable) in subtables)
        {
            bytes.AddRange(subtable);
        }

        return [.. bytes];
    }

    /// <summary>A format 4 subtable, with the final <c>0xFFFF</c> segment added.</summary>
    public static byte[] Format4(params TestSegment[] segments) => Format4(terminal: true, segments);

    /// <summary>A format 4 subtable, with the final <c>0xFFFF</c> segment added only when <paramref name="terminal"/>.</summary>
    public static byte[] Format4(bool terminal, params TestSegment[] segments)
    {
        var all = terminal ? [.. segments, new TestSegment(0xFFFF, 0xFFFF, 1)] : segments;
        var count = all.Length;
        var bytes = new List<byte>();
        Put16(bytes, 4);
        Put16(bytes, 0); // length, set below
        Put16(bytes, 0); // language
        Put16(bytes, (ushort)(count * 2));
        var power = 1;
        var log = 0;
        while (power * 2 <= count)
        {
            power *= 2;
            log++;
        }

        Put16(bytes, (ushort)(power * 2));
        Put16(bytes, (ushort)log);
        Put16(bytes, (ushort)((count * 2) - (power * 2)));
        foreach (var segment in all)
        {
            Put16(bytes, segment.End);
        }

        Put16(bytes, 0); // reservedPad
        foreach (var segment in all)
        {
            Put16(bytes, segment.Start);
        }

        foreach (var segment in all)
        {
            Put16(bytes, (ushort)segment.Delta);
        }

        // idRangeOffset[i] sits at 16 + 6 × count + 2 × i, and the glyphs of segment i start at 16 + 8 × count + 2 × k,
        // where k counts the glyphs of the segments before it.
        var glyphsBefore = 0;
        for (var i = 0; i < count; i++)
        {
            var segment = all[i];
            var computed = segment.Glyphs is null ? 0 : (2 * count) + (2 * glyphsBefore) - (2 * i);
            Put16(bytes, segment.RangeOffset ?? (ushort)computed);
            glyphsBefore += segment.Glyphs?.Length ?? 0;
        }

        foreach (var segment in all)
        {
            foreach (var glyph in segment.Glyphs ?? [])
            {
                Put16(bytes, glyph);
            }
        }

        var result = bytes.ToArray();
        BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(2), (ushort)result.Length);
        return result;
    }

    /// <summary>A format 12 subtable with the groups in the order given.</summary>
    public static byte[] Format12(params (uint Start, uint End, uint StartGlyph)[] groups)
    {
        var bytes = new List<byte>();
        Put16(bytes, 12);
        Put16(bytes, 0);
        Put32(bytes, (uint)(16 + (12 * groups.Length)));
        Put32(bytes, 0); // language
        Put32(bytes, (uint)groups.Length);
        foreach (var (start, end, startGlyph) in groups)
        {
            Put32(bytes, start);
            Put32(bytes, end);
            Put32(bytes, startGlyph);
        }

        return [.. bytes];
    }

    /// <summary>A 10-byte subtable of another format, for selection tests. Its content is never read.</summary>
    public static byte[] Format(ushort format) => [(byte)(format >> 8), (byte)format, 0, 10, 0, 0, 0, 0, 0, 0];

    internal static void Put16(List<byte> bytes, ushort value)
    {
        bytes.Add((byte)(value >> 8));
        bytes.Add((byte)value);
    }

    internal static void Put32(List<byte> bytes, uint value)
    {
        Put16(bytes, (ushort)(value >> 16));
        Put16(bytes, (ushort)value);
    }
}
