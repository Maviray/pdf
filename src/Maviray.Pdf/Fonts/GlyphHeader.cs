namespace Maviray.Pdf.Fonts;

/// <summary>The 10-byte header at the start of a glyph's data in <c>glyf</c>.</summary>
internal readonly record struct GlyphHeader(short ContourCount, GlyphBounds Bounds)
{
    /// <summary>A composite glyph is built from other glyphs (spec F-05).</summary>
    public bool IsComposite => ContourCount < 0;
}
