namespace Maviray.Pdf.Writer;

/// <summary>How a <see cref="PdfString"/> is written: <c>(…)</c> with escapes, or <c>&lt;…&gt;</c> in hex.</summary>
internal enum PdfStringForm
{
    Literal,
    Hex,
}
