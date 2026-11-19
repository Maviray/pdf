namespace Maviray.Pdf.Writer;

/// <summary>A PDF object that writes its own bytes (spec D-02).</summary>
internal abstract class PdfObject
{
    /// <summary>Writes the object's PDF syntax, with no leading or trailing whitespace.</summary>
    public abstract void WriteTo(PdfOutput output);
}
