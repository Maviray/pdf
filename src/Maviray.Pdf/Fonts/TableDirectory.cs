using System.Buffers.Binary;
using System.Text;

namespace Maviray.Pdf.Fonts;

/// <summary>
/// The table directory at the start of a font file: its sfnt version and where each table lies (spec F-02). Collections
/// and web fonts are refused here, before any table is read. Table checksums aren't verified, because many fonts in
/// use carry wrong ones.
/// </summary>
internal sealed class TableDirectory
{
    public const uint TrueTypeVersion = 0x00010000;
    public const uint AppleTrueTypeVersion = 0x74727565; // 'true'
    public const uint CffVersion = 0x4F54544F; // 'OTTO'
    private const uint CollectionVersion = 0x74746366; // 'ttcf'
    private const uint WoffVersion = 0x774F4646; // 'wOFF'
    private const uint Woff2Version = 0x774F4632; // 'wOF2'

    private readonly Dictionary<string, (int Offset, int Length)> _tables;

    private TableDirectory(uint sfntVersion, Dictionary<string, (int Offset, int Length)> tables, string[] tags)
    {
        SfntVersion = sfntVersion;
        _tables = tables;
        Tags = tags;
    }

    public uint SfntVersion { get; }

    /// <summary>The tags of the tables, in the order the directory lists them.</summary>
    public IReadOnlyList<string> Tags { get; }

    /// <exception cref="NotSupportedException">The file is a font collection or a web font.</exception>
    /// <exception cref="InvalidDataException">The file isn't a font, or its directory is damaged.</exception>
    public static TableDirectory Read(ReadOnlySpan<byte> file)
    {
        if (file.Length < 4)
        {
            throw new InvalidDataException($"The file is too short to be a font: it has {file.Length} bytes.");
        }

        var version = BinaryPrimitives.ReadUInt32BigEndian(file);
        switch (version)
        {
            case CollectionVersion:
                throw new NotSupportedException(
                    "The file is a TrueType font collection (.ttc), which isn't supported. Use a single font file.");
            case WoffVersion or Woff2Version:
                throw new NotSupportedException(
                    "The file is a web font (WOFF or WOFF2), which isn't supported. Use the font's .ttf file.");
            case TrueTypeVersion or AppleTrueTypeVersion or CffVersion:
                break;
            default:
                throw new InvalidDataException($"The file isn't a TrueType or OpenType font: its version is 0x{version:X8}.");
        }

        if (file.Length < 12)
        {
            throw new InvalidDataException($"The table directory is too short: it needs 12 bytes and the file has {file.Length}.");
        }

        var count = BinaryPrimitives.ReadUInt16BigEndian(file[4..]);
        var recordsEnd = 12 + (16 * count);
        if (recordsEnd > file.Length)
        {
            throw new InvalidDataException(
                $"The table directory lists {count} tables, which need {recordsEnd} bytes; the file has {file.Length}.");
        }

        var tables = new Dictionary<string, (int Offset, int Length)>(count, StringComparer.Ordinal);
        var tags = new string[count];
        for (var i = 0; i < count; i++)
        {
            var record = file.Slice(12 + (16 * i), 16);
            var tag = Encoding.Latin1.GetString(record[..4]);
            var offset = BinaryPrimitives.ReadUInt32BigEndian(record[8..]);
            var length = BinaryPrimitives.ReadUInt32BigEndian(record[12..]);
            if ((ulong)offset + length > (ulong)file.Length)
            {
                throw new InvalidDataException(
                    $"The '{tag}' table lies outside the file: it runs from {offset} to {(ulong)offset + length}, and the file has {file.Length} bytes.");
            }

            if (!tables.TryAdd(tag, ((int)offset, (int)length)))
            {
                throw new InvalidDataException($"The table directory lists the '{tag}' table twice.");
            }

            tags[i] = tag;
        }

        return new TableDirectory(version, tables, tags);
    }

    public bool HasTable(string tag) => _tables.ContainsKey(tag);

    /// <summary>The bytes of <paramref name="tag"/> within <paramref name="file"/>, or an empty span when it's absent.</summary>
    public ReadOnlySpan<byte> GetTable(ReadOnlySpan<byte> file, string tag) =>
        _tables.TryGetValue(tag, out var table) ? file.Slice(table.Offset, table.Length) : default;
}
