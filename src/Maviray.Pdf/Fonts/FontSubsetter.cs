using System.Buffers.Binary;

namespace Maviray.Pdf.Fonts;

/// <summary>
/// Builds the subset of a font that a document used (spec F-08). Subset glyph <i>n</i> is the original glyph of CID
/// <i>n</i>, so the codes in the content streams are the subset's glyph IDs; the components they need follow. Glyph
/// bytes are copied with only their component IDs rewritten. <c>loca</c>, <c>hmtx</c>, <c>hhea</c>, <c>maxp</c>,
/// <c>head</c> and <c>post</c> are rebuilt, the hinting tables are kept, and every other table is dropped. The bytes
/// depend only on the font and the list.
/// </summary>
internal static class FontSubsetter
{
    private const uint TrueTypeVersion = 0x00010000;
    private const int PostHeaderLength = 32;
    private static readonly string[] CopiedTables = ["cvt ", "fpgm", "prep", "gasp"];

    /// <param name="font">The font, which must allow subsetting (F-06).</param>
    /// <param name="glyphs">The original glyph of each CID, from CID 0, which is <c>.notdef</c> (glyph 0).</param>
    /// <exception cref="InvalidDataException">A glyph the subset reaches is damaged (F-05). The message starts with the
    /// font's PostScript name.</exception>
    /// <exception cref="InvalidOperationException">The font may not be subset, or the subset would pass 65,535 glyphs
    /// or the size of one file.</exception>
    public static FontSubset Build(TrueTypeFont font, IReadOnlyList<ushort> glyphs)
    {
        ArgumentNullException.ThrowIfNull(font);
        if (font.Embedding == FontEmbedding.Whole)
        {
            throw new InvalidOperationException(
                $"{font.PostScriptName ?? "The font"} may not be subset: its OS/2 fsType sets bit 8 (No subsetting), so it is embedded whole.");
        }

        if (font.PostScriptName is not { } name)
        {
            return BuildSubset(font, glyphs);
        }

        try
        {
            return BuildSubset(font, glyphs);
        }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"{name}: {error.Message}", error);
        }
    }

    private static FontSubset BuildSubset(TrueTypeFont font, IReadOnlyList<ushort> glyphs)
    {
        var originals = GlyphClosure.Resolve(font, glyphs);
        var tables = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["head"] = Copy(font, "head"),
            ["hhea"] = Copy(font, "hhea"),
            ["maxp"] = Copy(font, "maxp"),
        };
        BinaryPrimitives.WriteInt16BigEndian(tables["head"].AsSpan(50), 1); // indexToLocFormat: long
        BinaryPrimitives.WriteUInt16BigEndian(tables["hhea"].AsSpan(34), (ushort)originals.Length); // numberOfHMetrics
        BinaryPrimitives.WriteUInt16BigEndian(tables["maxp"].AsSpan(4), (ushort)originals.Length); // numGlyphs
        if (font.TryGetTable("post", out var post))
        {
            var header = post[..PostHeaderLength].ToArray();
            BinaryPrimitives.WriteUInt32BigEndian(header, 0x00030000); // format 3: no glyph names
            tables["post"] = header;
        }

        foreach (var tag in CopiedTables)
        {
            if (font.TryGetTable(tag, out var table))
            {
                tables[tag] = table.ToArray();
            }
        }

        // Size the file before copying any glyph, so a subset too large for one array is refused, not attempted.
        long glyfLength = 0;
        foreach (var glyph in originals)
        {
            glyfLength += Pad(font.GetGlyphData(glyph).Length);
        }

        var tableCount = tables.Count + 3;
        var fileLength = 12L + (16L * tableCount) + glyfLength + (4L * (originals.Length + 1)) + (4L * originals.Length);
        fileLength += tables.Values.Sum(t => Pad(t.Length));
        if (fileLength > Array.MaxLength)
        {
            throw new InvalidOperationException(
                $"The font's subset would be {fileLength} bytes, too large for one file: at most {Array.MaxLength} bytes fit.");
        }

        var firstIndex = new Dictionary<ushort, ushort>();
        for (var i = 0; i < originals.Length; i++)
        {
            firstIndex.TryAdd(originals[i], (ushort)i);
        }

        var glyf = new byte[glyfLength];
        var loca = new byte[4 * (originals.Length + 1)];
        var hmtx = new byte[4 * originals.Length];
        var offset = 0;
        for (var i = 0; i < originals.Length; i++)
        {
            var data = font.GetGlyphData(originals[i]);
            var copy = glyf.AsSpan(offset, data.Length);
            data.CopyTo(copy);
            foreach (var component in CompositeGlyph.ReadComponents(data, font.GlyphCount))
            {
                BinaryPrimitives.WriteUInt16BigEndian(copy[component.GlyphIdOffset..], firstIndex[component.GlyphId]);
            }

            BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan(4 * i), (uint)offset);
            BinaryPrimitives.WriteUInt16BigEndian(hmtx.AsSpan(4 * i), (ushort)font.GetAdvanceWidth(originals[i]));
            BinaryPrimitives.WriteInt16BigEndian(hmtx.AsSpan((4 * i) + 2), (short)font.GetLeftSideBearing(originals[i]));
            offset += (int)Pad(data.Length);
        }

        BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan(4 * originals.Length), (uint)offset);
        tables["glyf"] = glyf;
        tables["loca"] = loca;
        tables["hmtx"] = hmtx;
        return new FontSubset(FontFileAssembler.Assemble(TrueTypeVersion, tables), originals);
    }

    private static byte[] Copy(TrueTypeFont font, string tag)
    {
        _ = font.TryGetTable(tag, out var table); // a required table: the font wouldn't have loaded without it
        return table.ToArray();
    }

    private static long Pad(long length) => (length + 3) & ~3L;
}
