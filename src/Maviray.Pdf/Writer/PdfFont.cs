namespace Maviray.Pdf.Writer;

/// <summary>
/// A font a page can set: one of the standard 14 fonts (<see cref="StandardFont"/>), or a TrueType font embedded in the
/// document (<see cref="EmbeddedFont"/>). Both share one sequence of resource names, and each writes its objects once,
/// when the document closes (spec F-10, P0 D-10).
/// </summary>
internal abstract class PdfFont
{
    /// <summary>Writes the font's objects, with its font dictionary at <paramref name="reference"/>.</summary>
    public abstract void Write(PdfFileWriter file, PdfReference reference);
}
