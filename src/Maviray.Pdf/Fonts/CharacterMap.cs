namespace Maviray.Pdf.Fonts;

/// <summary>
/// A font's Unicode character to glyph map, read from one <c>cmap</c> subtable (spec F-04). Every check runs when the
/// subtable is parsed, so a lookup never reads outside it. A lookup returns glyph 0 for a code point the font doesn't
/// map, for a surrogate, for a value outside Unicode, and for a mapping to a glyph the font doesn't have.
/// </summary>
internal abstract class CharacterMap
{
    // The first subtable present in this order, and in this format, wins.
    private static readonly (ushort Platform, ushort Encoding, ushort Format)[] Preference =
    [
        (3, 10, 12),
        (0, 4, 12),
        (3, 1, 4),
        (0, 0, 4),
        (0, 1, 4),
        (0, 2, 4),
        (0, 3, 4),
    ];

    /// <exception cref="NotSupportedException">No subtable in the preference order exists.</exception>
    /// <exception cref="InvalidDataException">The chosen subtable is damaged.</exception>
    public static CharacterMap Parse(ReadOnlySpan<byte> cmapTable, int glyphCount)
    {
        var reader = new FontTableReader(cmapTable, "cmap");
        foreach (var (platform, encoding, format) in Preference)
        {
            if (FindSubtable(reader, platform, encoding) is { } offset && reader.UInt16(offset) == format)
            {
                return ParseAt(cmapTable, offset, format, glyphCount);
            }
        }

        throw new NotSupportedException(
            "The font has no Unicode 'cmap' subtable in format 4 or 12, so its characters can't be mapped to glyphs.");
    }

    /// <summary>The subtable for one platform and encoding, in format 4 or 12, or null when there is none.</summary>
    public static CharacterMap? ParseSubtable(ReadOnlySpan<byte> cmapTable, ushort platformId, ushort encodingId, int glyphCount)
    {
        var reader = new FontTableReader(cmapTable, "cmap");
        if (FindSubtable(reader, platformId, encodingId) is not { } offset)
        {
            return null;
        }

        var format = reader.UInt16(offset);
        return format is 4 or 12 ? ParseAt(cmapTable, offset, format, glyphCount) : null;
    }

    public ushort GetGlyphId(int codePoint) =>
        codePoint is < 0 or > 0x10FFFF or (>= 0xD800 and <= 0xDFFF) ? (ushort)0 : Lookup(codePoint);

    /// <summary>Every mapped code point and its glyph, in ascending code point order, without glyph 0.</summary>
    public abstract IEnumerable<(int CodePoint, ushort GlyphId)> Mappings();

    /// <summary>The glyph for a code point that is a Unicode scalar value.</summary>
    protected abstract ushort Lookup(int codePoint);

    private static CharacterMap ParseAt(ReadOnlySpan<byte> cmapTable, int offset, ushort format, int glyphCount)
    {
        // A subtable runs from its offset to the end of the table: the format 4 length field isn't trusted.
        var subtable = cmapTable[offset..];
        return format == 4 ? Format4CharacterMap.ParseSubtableBytes(subtable, glyphCount) : Format12CharacterMap.ParseSubtableBytes(subtable, glyphCount);
    }

    private static int? FindSubtable(FontTableReader reader, ushort platformId, ushort encodingId)
    {
        var count = reader.UInt16(2);
        for (var i = 0; i < count; i++)
        {
            var record = 4 + (8 * i);
            if (reader.UInt16(record) != platformId || reader.UInt16(record + 2) != encodingId)
            {
                continue;
            }

            var offset = reader.UInt32(record + 4);
            if (reader.Length < 2 || offset > (uint)reader.Length - 2)
            {
                throw reader.Error($"places the ({platformId}, {encodingId}) subtable at {offset}, outside its {reader.Length} bytes");
            }

            return (int)offset;
        }

        return null;
    }
}
