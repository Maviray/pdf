namespace Maviray.Pdf.Fonts;

/// <summary>
/// A subset built by <see cref="FontSubsetter"/>: a TrueType file whose glyph <i>n</i> is the original glyph
/// <c>OriginalGlyphIds[n]</c>. The first entries are the CIDs a document drew, and the rest are the components they
/// need (spec F-08).
/// </summary>
internal sealed class FontSubset
{
    public FontSubset(byte[] data, ushort[] originalGlyphIds)
    {
        Data = data;
        OriginalGlyphIds = Array.AsReadOnly(originalGlyphIds);
    }

    /// <summary>The TrueType file, for the <c>FontFile2</c> stream.</summary>
    public ReadOnlyMemory<byte> Data { get; }

    /// <summary>The original glyph of every subset glyph.</summary>
    public IReadOnlyList<ushort> OriginalGlyphIds { get; }
}
