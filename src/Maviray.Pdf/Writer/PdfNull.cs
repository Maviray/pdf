namespace Maviray.Pdf.Writer;

/// <summary>The PDF null object, written as <c>null</c>.</summary>
internal sealed class PdfNull : PdfObject
{
    private PdfNull()
    {
    }

    public static PdfNull Instance { get; } = new();

    public override void WriteTo(PdfOutput output) => output.Write("null"u8);
}
