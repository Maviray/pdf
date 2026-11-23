using System.Buffers.Binary;

namespace Maviray.Pdf.Fonts;

/// <summary>
/// Reads big-endian values from one table's bytes. Every read is checked against the table's length, and a read past
/// the end throws an <see cref="InvalidDataException"/> that names the table (spec F-02, F-03).
/// </summary>
internal readonly ref struct FontTableReader
{
    private readonly ReadOnlySpan<byte> _data;
    private readonly string _tag;

    public FontTableReader(ReadOnlySpan<byte> data, string tag)
    {
        _data = data;
        _tag = tag;
    }

    public int Length => _data.Length;

    public ushort UInt16(int offset) => BinaryPrimitives.ReadUInt16BigEndian(Need(offset, 2));

    public short Int16(int offset) => BinaryPrimitives.ReadInt16BigEndian(Need(offset, 2));

    public uint UInt32(int offset) => BinaryPrimitives.ReadUInt32BigEndian(Need(offset, 4));

    public int Int32(int offset) => BinaryPrimitives.ReadInt32BigEndian(Need(offset, 4));

    public ReadOnlySpan<byte> Slice(int offset, int length) => Need(offset, length);

    /// <summary>An exception whose message reads "The '<c>tag</c>' table <paramref name="problem"/>."</summary>
    public InvalidDataException Error(string problem) => new($"The '{_tag}' table {problem}.");

    private ReadOnlySpan<byte> Need(int offset, int length)
    {
        if (offset < 0 || length < 0)
        {
            throw Error($"has a negative offset or length ({offset}, {length})");
        }

        var end = (long)offset + length;
        if (end > _data.Length)
        {
            throw Error($"is too short: it needs {end} bytes and has {_data.Length}");
        }

        return _data.Slice(offset, length);
    }
}
