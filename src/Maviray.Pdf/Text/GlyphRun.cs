namespace Maviray.Pdf.Text;

/// <summary>
/// The glyphs of one span on one line, in order (spec T-14). <see cref="X"/> is its first glyph's position from the
/// paragraph's left edge; the drawing side looks up the span's font, size and colour by <see cref="SpanIndex"/>.
/// </summary>
internal sealed class GlyphRun(int spanIndex, double x, IReadOnlyList<PositionedGlyph> glyphs)
{
    public int SpanIndex { get; } = spanIndex;

    public double X { get; } = x;

    public IReadOnlyList<PositionedGlyph> Glyphs { get; } = glyphs;
}
