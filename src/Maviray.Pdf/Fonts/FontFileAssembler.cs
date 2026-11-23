using System.Buffers.Binary;

namespace Maviray.Pdf.Fonts;

/// <summary>
/// Writes tables as a TrueType file, laid out as the OpenType specification's "Organization of an OpenType font" says
/// (spec F-08 step 6). The records are in ascending tag order. Each table starts on a 4-byte boundary, is padded with
/// zeros, and has a correct checksum. <c>head.checkSumAdjustment</c> makes the whole file sum to <c>0xB1B0AFBA</c>.
/// </summary>
internal static class FontFileAssembler
{
    private const uint FileChecksum = 0xB1B0AFBA;
    private const int HeaderLength = 12;
    private const int RecordLength = 16;
    private const int AdjustmentOffset = 8;

    /// <summary>The sum, modulo 2³², of the data as big-endian 32-bit words, the last one padded with zeros.</summary>
    public static uint Checksum(ReadOnlySpan<byte> data)
    {
        uint sum = 0;
        var whole = data.Length & ~3;
        for (var i = 0; i < whole; i += 4)
        {
            sum = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(data[i..]));
        }

        if (whole < data.Length)
        {
            Span<byte> last = stackalloc byte[4];
            last.Clear();
            data[whole..].CopyTo(last);
            sum = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(last));
        }

        return sum;
    }

    /// <param name="sfntVersion">The file's version, such as <c>0x00010000</c> for TrueType outlines.</param>
    /// <param name="tables">Each table's bytes by tag. The arrays aren't changed.</param>
    /// <exception cref="ArgumentException">There are no tables, a tag isn't four printable ASCII characters, or the
    /// file would be too large for one array.</exception>
    public static byte[] Assemble(uint sfntVersion, IReadOnlyDictionary<string, byte[]> tables)
    {
        ArgumentNullException.ThrowIfNull(tables);
        if (tables.Count == 0)
        {
            throw new ArgumentException("A font file needs at least one table.", nameof(tables));
        }

        var tags = tables.Keys.ToArray();
        Array.Sort(tags, StringComparer.Ordinal);
        long length = HeaderLength + ((long)RecordLength * tags.Length);
        var offsets = new long[tags.Length];
        for (var i = 0; i < tags.Length; i++)
        {
            if (tags[i].Length != 4 || tags[i].Any(c => c is < (char)0x20 or > (char)0x7E))
            {
                throw new ArgumentException($"'{tags[i]}' isn't a table tag: a tag is four characters from 0x20 to 0x7E.", nameof(tables));
            }

            offsets[i] = length;
            length = Pad(length + tables[tags[i]].Length);
        }

        if (length > Array.MaxLength)
        {
            throw new ArgumentException($"The font file would be {length} bytes, more than one array can hold.", nameof(tables));
        }

        var file = new byte[length];
        var span = file.AsSpan();
        BinaryPrimitives.WriteUInt32BigEndian(span, sfntVersion);
        BinaryPrimitives.WriteUInt16BigEndian(span[4..], (ushort)tags.Length);
        var power = 1;
        var log = 0;
        while (power * 2 <= tags.Length)
        {
            power *= 2;
            log++;
        }

        BinaryPrimitives.WriteUInt16BigEndian(span[6..], (ushort)(power * RecordLength));
        BinaryPrimitives.WriteUInt16BigEndian(span[8..], (ushort)log);
        BinaryPrimitives.WriteUInt16BigEndian(span[10..], (ushort)((tags.Length - power) * RecordLength));

        var headOffset = -1;
        for (var i = 0; i < tags.Length; i++)
        {
            var data = tables[tags[i]];
            var offset = (int)offsets[i];
            var table = span.Slice(offset, data.Length);
            data.CopyTo(table);
            if (tags[i] == "head" && data.Length >= AdjustmentOffset + 4)
            {
                table.Slice(AdjustmentOffset, 4).Clear();
                headOffset = offset;
            }

            var record = span.Slice(HeaderLength + (RecordLength * i), RecordLength);
            for (var c = 0; c < 4; c++)
            {
                record[c] = (byte)tags[i][c];
            }

            BinaryPrimitives.WriteUInt32BigEndian(record[4..], Checksum(table));
            BinaryPrimitives.WriteUInt32BigEndian(record[8..], (uint)offset);
            BinaryPrimitives.WriteUInt32BigEndian(record[12..], (uint)data.Length);
        }

        if (headOffset >= 0)
        {
            BinaryPrimitives.WriteUInt32BigEndian(span[(headOffset + AdjustmentOffset)..], unchecked(FileChecksum - Checksum(file)));
        }

        return file;
    }

    private static long Pad(long length) => (length + 3) & ~3L;
}
