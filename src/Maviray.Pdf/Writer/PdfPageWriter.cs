namespace Maviray.Pdf.Writer;

/// <summary>The page that <see cref="PdfDocumentWriter"/> has open, with the builder for its content stream.</summary>
internal sealed class PdfPageWriter
{
    internal PdfPageWriter(double width, double height, FontResources fonts)
    {
        Width = width;
        Height = height;
        Content = new ContentStreamBuilder(fonts);
    }

    /// <summary>The page width in points.</summary>
    public double Width { get; }

    /// <summary>The page height in points.</summary>
    public double Height { get; }

    /// <summary>The page's content stream. <see cref="PdfDocumentWriter.EndPage"/> finishes and writes it.</summary>
    public ContentStreamBuilder Content { get; }
}
