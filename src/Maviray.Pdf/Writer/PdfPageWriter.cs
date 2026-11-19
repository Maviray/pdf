namespace Maviray.Pdf.Writer;

/// <summary>The page that <see cref="PdfDocumentWriter"/> has open. P0.3 adds its content stream.</summary>
internal sealed class PdfPageWriter
{
    internal PdfPageWriter(double width, double height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>The page width in points.</summary>
    public double Width { get; }

    /// <summary>The page height in points.</summary>
    public double Height { get; }
}
