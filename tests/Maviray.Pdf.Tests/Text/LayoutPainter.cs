using Maviray.Pdf.Fonts;
using Maviray.Pdf.Text;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Text;

/// <summary>
/// Draws a text layout on a PDF page, as the text gate and the layout tests need (spec T-15): each glyph run with
/// <c>ShowGlyphs</c> in its <c>TJ</c> form, at the run's x and the line's baseline, and each decoration as a filled
/// rectangle. The page's y grows upward, so every position is flipped from the layout's.
/// </summary>
internal static class LayoutPainter
{
    public static void Paint(
        PdfPageWriter page,
        TextLayout layout,
        IReadOnlyDictionary<TrueTypeFont, EmbeddedFont> fonts,
        double left,
        double top)
    {
        var spans = layout.Source.Source.Spans;
        var content = page.Content;
        foreach (var line in layout.Lines)
        {
            var baseline = page.Height - (top + line.Top + line.Baseline);
            foreach (var run in line.Runs)
            {
                var style = spans[run.SpanIndex].Style;
                var font = fonts[style.Font];
                var cids = new ushort[run.Glyphs.Count];
                var extras = new double[run.Glyphs.Count];
                for (var i = 0; i < cids.Length; i++)
                {
                    cids[i] = font.Encode(run.Glyphs[i].GlyphId, run.Glyphs[i].CodePoint);
                    extras[i] = run.Glyphs[i].ExtraAdvance;
                }

                content.BeginText()
                    .SetFont(font, style.Size)
                    .SetTextMatrix(1, 0, 0, 1, left + run.X, baseline)
                    .ShowGlyphs(cids, extras)
                    .EndText();
            }

            foreach (var decoration in line.Decorations)
            {
                var bottom = page.Height - (top + line.Top + decoration.Top + decoration.Thickness);
                content.Rectangle(left + decoration.X, bottom, decoration.Width, decoration.Thickness).Fill();
            }
        }
    }
}
