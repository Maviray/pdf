namespace Maviray.Pdf.Fonts;

/// <summary>
/// One component of a composite glyph: the glyph it uses, and where that glyph ID is stored in the composite's data,
/// so a subset can rewrite it in place (spec F-05, F-08).
/// </summary>
internal readonly record struct GlyphComponent(ushort GlyphId, int GlyphIdOffset);
