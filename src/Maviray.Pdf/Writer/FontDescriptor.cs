using Maviray.Pdf.Fonts;

namespace Maviray.Pdf.Writer;

/// <summary>
/// The font descriptor of an embedded font (spec F-09). Lengths come from <c>head</c>, <c>hhea</c> and <c>OS/2</c>,
/// converted to the PDF's 1000-unit glyph space with four decimals. <c>/StemV</c> is an estimate from the weight: no
/// table gives the real value, and a viewer uses it only to substitute a missing font, which never happens to an
/// embedded one.
/// </summary>
internal static class FontDescriptor
{
    private const int Symbolic = 4;
    private const int FixedPitch = 1;
    private const int Italic = 64;

    /// <param name="font">The font being embedded.</param>
    /// <param name="fontName">The same name as the font's <c>/BaseFont</c>.</param>
    /// <param name="fontFile">The <c>FontFile2</c> stream.</param>
    /// <exception cref="InvalidDataException">The glyph for "H", which supplies a missing cap height, is damaged.</exception>
    public static PdfDictionary Create(TrueTypeFont font, string fontName, PdfReference fontFile)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(fontFile);
        var flags = Symbolic
            | (font.IsFixedPitch ? FixedPitch : 0)
            | (font.IsItalic || font.ItalicAngle != 0 ? Italic : 0);
        var bounds = font.FontBounds;
        var weight = Math.Clamp(font.WeightClass, 100, 900);
        var stemV = (int)Math.Round(10 + (220.0 * (weight - 50) / 900), MidpointRounding.AwayFromZero);

        return new PdfDictionary()
            .Set(PdfName.Type, PdfName.FontDescriptor)
            .Set(PdfName.FontName, new PdfName(fontName))
            .Set(PdfName.Flags, new PdfInteger(flags))
            .Set(PdfName.FontBBox, new PdfArray(
                Scaled(font, bounds.XMin), Scaled(font, bounds.YMin), Scaled(font, bounds.XMax), Scaled(font, bounds.YMax)))
            .Set(PdfName.ItalicAngle, new PdfReal(font.ItalicAngle))
            .Set(PdfName.Ascent, Scaled(font, font.HorizontalHeaderMetrics.Ascender))
            .Set(PdfName.Descent, Scaled(font, font.HorizontalHeaderMetrics.Descender))
            .Set(PdfName.CapHeight, Scaled(font, CapHeight(font)))
            .Set(PdfName.StemV, new PdfInteger(stemV))
            .Set(PdfName.FontFile2, fontFile);
    }

    /// <summary><c>OS/2.sCapHeight</c>; otherwise the top of the glyph for "H", if it has an outline; otherwise the ascent.</summary>
    private static int CapHeight(TrueTypeFont font)
    {
        if (font.CapHeight is { } capHeight)
        {
            return capHeight;
        }

        var glyph = font.GetGlyphId('H');
        return glyph != 0 && font.GetGlyphHeader(glyph) is { } header ? header.Bounds.YMax : font.HorizontalHeaderMetrics.Ascender;
    }

    private static PdfReal Scaled(TrueTypeFont font, int value) => new(value * 1000.0 / font.UnitsPerEm);
}
