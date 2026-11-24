namespace Maviray.Pdf.Text;

/// <summary>
/// A glyph that preparation resolved for a code point (spec T-07): its glyph ID, the code point it copies out as, and
/// its advance in points at the span's size.
/// </summary>
internal readonly record struct PreparedGlyph(ushort GlyphId, int CodePoint, double Advance);
