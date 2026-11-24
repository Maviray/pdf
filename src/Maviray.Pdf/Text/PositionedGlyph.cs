namespace Maviray.Pdf.Text;

/// <summary>
/// A glyph placed on a line (spec T-14): its glyph ID, the code point it copies out as, its x position from the
/// paragraph's left edge, and the extra advance after it, which is the letter spacing after its cluster.
/// </summary>
internal readonly record struct PositionedGlyph(ushort GlyphId, int CodePoint, double X, double ExtraAdvance);
