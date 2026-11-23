namespace Maviray.Pdf.Fonts;

/// <summary>
/// Walks the component records of a composite glyph (spec F-05). Only the flags that change a record's length are
/// honoured, to find each component's glyph ID; arguments, transforms and instructions are never interpreted. Cycles and
/// nesting depth are the subset's closure rules, because a component is only followed when a subset is built.
/// </summary>
internal static class CompositeGlyph
{
    private const ushort ArgumentsAreWords = 0x0001;
    private const ushort HasScale = 0x0008;
    private const ushort MoreComponents = 0x0020;
    private const ushort HasXAndYScale = 0x0040;
    private const ushort HasTwoByTwo = 0x0080;
    private const int HeaderLength = 10;

    /// <returns>The components in record order; an empty array for an empty or simple glyph.</returns>
    /// <exception cref="InvalidDataException">A record is cut short, or names a glyph past the font.</exception>
    public static GlyphComponent[] ReadComponents(ReadOnlySpan<byte> glyphData, int glyphCount)
    {
        if (glyphData.IsEmpty)
        {
            return [];
        }

        var reader = new FontTableReader(glyphData, "glyf");
        if (reader.Int16(0) >= 0)
        {
            return [];
        }

        var components = new List<GlyphComponent>();
        var position = HeaderLength;
        ushort flags;
        do
        {
            flags = reader.UInt16(position);
            var glyphId = reader.UInt16(position + 2);
            if (glyphId >= glyphCount)
            {
                throw reader.Error($"has a composite glyph whose component {glyphId} is past the font's {glyphCount} glyphs");
            }

            // The OpenType pseudo-code tests the transform flags in this order, so the first one set wins.
            var transform = (flags & HasScale) != 0 ? 2 : (flags & HasXAndYScale) != 0 ? 4 : (flags & HasTwoByTwo) != 0 ? 8 : 0;
            var length = 4 + ((flags & ArgumentsAreWords) != 0 ? 4 : 2) + transform;
            _ = reader.Slice(position, length); // the whole record must be present
            components.Add(new GlyphComponent(glyphId, position + 2));
            position += length;
        }
        while ((flags & MoreComponents) != 0);

        return [.. components];
    }
}
