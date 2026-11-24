namespace Maviray.Pdf.Text;

/// <summary>
/// A code point that the font of its span can't draw, so it is drawn as <c>.notdef</c> (spec T-07). The layout spec
/// turns these into diagnostics, and font fallback arrives in P2.
/// </summary>
internal readonly record struct MissingGlyph(int CodePoint, int SpanIndex);
