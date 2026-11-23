namespace Maviray.Pdf.Writer;

/// <summary>
/// One of the standard 14 fonts every PDF reader supplies, so nothing is embedded (spec D-10). Only Helvetica exists in
/// P0. Its text is encoded with <see cref="Writer.WinAnsiEncoding"/>.
/// </summary>
internal sealed class StandardFont : PdfFont
{
    private StandardFont(string baseFont)
    {
        BaseFont = new PdfName(baseFont);
    }

    public static StandardFont Helvetica { get; } = new("Helvetica");

    public PdfName BaseFont { get; }

    /// <summary>
    /// The font dictionary. It has no <c>/Widths</c> and no font descriptor, which PDF 1.7 allows for the standard 14
    /// fonts; the embedded TrueType fonts of P1 carry both.
    /// </summary>
    public PdfDictionary CreateDictionary() => new PdfDictionary()
        .Set(PdfName.Type, PdfName.Font)
        .Set(PdfName.Subtype, PdfName.Type1)
        .Set(PdfName.BaseFont, BaseFont)
        .Set(PdfName.Encoding, PdfName.WinAnsiEncoding);

    /// <summary>Writes the font dictionary at <paramref name="reference"/>: a standard font has no other objects.</summary>
    public override void Write(PdfFileWriter file, PdfReference reference)
    {
        ArgumentNullException.ThrowIfNull(file);
        file.Write(reference, CreateDictionary());
    }
}
