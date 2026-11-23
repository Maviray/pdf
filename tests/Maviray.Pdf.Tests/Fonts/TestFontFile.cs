using System.Buffers.Binary;
using System.Text;
using Maviray.Pdf.Fonts;

namespace Maviray.Pdf.Tests.Fonts;

/// <summary>
/// Reads a TrueType file the tests produce, such as a subset, straight from its bytes with
/// <see cref="BinaryPrimitives"/>, so the checks don't depend on <see cref="TableDirectory"/>. It assumes the layout a
/// subset has: long <c>loca</c>, and one full <c>hmtx</c> entry per glyph.
/// </summary>
internal sealed class TestFontFile
{
    private readonly byte[] _file;
    private readonly List<string> _tags = [];
    private readonly Dictionary<string, (uint Checksum, int Offset, int Length)> _records = new(StringComparer.Ordinal);

    public TestFontFile(byte[] file)
    {
        _file = file;
        var count = BinaryPrimitives.ReadUInt16BigEndian(file.AsSpan(4));
        for (var i = 0; i < count; i++)
        {
            var record = file.AsSpan(12 + (16 * i), 16);
            var tag = Encoding.Latin1.GetString(record[..4]);
            _tags.Add(tag);
            _records[tag] = (
                BinaryPrimitives.ReadUInt32BigEndian(record[4..]),
                (int)BinaryPrimitives.ReadUInt32BigEndian(record[8..]),
                (int)BinaryPrimitives.ReadUInt32BigEndian(record[12..]));
        }
    }

    public uint SfntVersion => BinaryPrimitives.ReadUInt32BigEndian(_file);

    public (ushort SearchRange, ushort EntrySelector, ushort RangeShift) SearchFields => (
        BinaryPrimitives.ReadUInt16BigEndian(_file.AsSpan(6)),
        BinaryPrimitives.ReadUInt16BigEndian(_file.AsSpan(8)),
        BinaryPrimitives.ReadUInt16BigEndian(_file.AsSpan(10)));

    /// <summary>The tags in record order.</summary>
    public IReadOnlyList<string> Tags => _tags;

    public int GlyphCount => BinaryPrimitives.ReadUInt16BigEndian(Table("maxp").AsSpan(4));

    public int NumberOfHMetrics => BinaryPrimitives.ReadUInt16BigEndian(Table("hhea").AsSpan(34));

    /// <summary>The long <c>loca</c> entries, <see cref="GlyphCount"/> + 1 of them.</summary>
    public uint[] Loca
    {
        get
        {
            var loca = Table("loca");
            return Enumerable.Range(0, GlyphCount + 1).Select(i => BinaryPrimitives.ReadUInt32BigEndian(loca.AsSpan(4 * i))).ToArray();
        }
    }

    public byte[] Table(string tag)
    {
        var (_, offset, length) = _records[tag];
        return _file.AsSpan(offset, length).ToArray();
    }

    public int OffsetOf(string tag) => _records[tag].Offset;

    public uint RecordChecksum(string tag) => _records[tag].Checksum;

    /// <summary>The glyph's bytes in <c>glyf</c>, padding included.</summary>
    public byte[] Glyph(int glyph)
    {
        var loca = Loca;
        return Table("glyf")[(int)loca[glyph]..(int)loca[glyph + 1]];
    }

    public (ushort Advance, short LeftSideBearing) Metrics(int glyph)
    {
        var hmtx = Table("hmtx").AsSpan(4 * glyph);
        return (BinaryPrimitives.ReadUInt16BigEndian(hmtx), BinaryPrimitives.ReadInt16BigEndian(hmtx[2..]));
    }

    public ushort[] ComponentIds(int glyph) =>
        CompositeGlyph.ReadComponents(Glyph(glyph), GlyphCount).Select(c => c.GlyphId).ToArray();
}
