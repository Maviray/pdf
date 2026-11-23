using Maviray.Pdf.Fonts;

namespace Maviray.Pdf.Writer;

/// <summary>
/// The <c>/W</c> array of an embedded font (spec F-09): every CID from 0 to the last one assigned, as one run,
/// <c>[0 [w0 w1 … wn]]</c>. CIDs are assigned in order of use, so there are no gaps to compress and no <c>/DW</c> is
/// needed. Widths are in the PDF's 1000-unit glyph space and keep four decimals.
/// </summary>
internal static class CidWidths
{
    /// <param name="font">The font the CIDs come from.</param>
    /// <param name="glyphs">The original glyph of each CID, from CID 0.</param>
    public static PdfArray Create(TrueTypeFont font, IReadOnlyList<ushort> glyphs)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(glyphs);
        var widths = new PdfArray();
        foreach (var glyph in glyphs)
        {
            widths.Add(new PdfReal(font.GetAdvanceWidth(glyph) * 1000.0 / font.UnitsPerEm));
        }

        return new PdfArray(new PdfInteger(0), widths);
    }
}
