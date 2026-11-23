using Maviray.Pdf.Fonts;

namespace Maviray.Pdf.Writer;

/// <summary>
/// Writes a document page by page on top of <see cref="PdfFileWriter"/> (spec D-06). Each page is written as soon as it
/// ends, and only its reference is kept. The page tree and the catalog are written at <see cref="Close"/>. The page tree
/// is flat in P0.
/// </summary>
internal sealed class PdfDocumentWriter : IDisposable
{
    private readonly PdfFileWriter _file;
    private readonly FontResources _fonts;
    private readonly PdfReference _pageTree;
    private readonly PdfArray _kids = [];
    private PdfPageWriter? _openPage;
    private bool _closed;

    public PdfDocumentWriter(Stream output, PdfWriterOptions? options = null)
    {
        _file = new PdfFileWriter(output, options ?? new PdfWriterOptions());
        _fonts = new FontResources(_file);

        // Reserved first, so every page dictionary can name its /Parent as soon as it is written.
        _pageTree = _file.Reserve();
    }

    /// <summary>
    /// This document's embedded font for <paramref name="font"/>, the same instance for the same font (spec F-10). It is
    /// written when the document closes, and only if a page set it.
    /// </summary>
    /// <exception cref="InvalidOperationException">The document is closed.</exception>
    public EmbeddedFont EmbedFont(TrueTypeFont font)
    {
        ThrowIfClosed();
        return _fonts.Embed(font);
    }

    /// <summary>Opens a page of <paramref name="width"/> by <paramref name="height"/> points.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A size isn't a positive, finite number.</exception>
    /// <exception cref="InvalidOperationException">A page is already open, or the document is closed.</exception>
    public PdfPageWriter BeginPage(double width, double height)
    {
        ThrowIfClosed();
        ThrowIfNotPositive(width, nameof(width));
        ThrowIfNotPositive(height, nameof(height));
        if (_openPage is not null)
        {
            throw new InvalidOperationException("A page is already open. End it before beginning the next one.");
        }

        _openPage = new PdfPageWriter(width, height, _fonts);
        return _openPage;
    }

    /// <summary>
    /// Writes the open page's content stream, then its dictionary, and releases the page (spec D-06). A page with no
    /// content gets no <c>/Contents</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException"><paramref name="page"/> isn't this document's open page, or its content
    /// has an open path or an unmatched <c>q</c>. Nothing is written then.</exception>
    public void EndPage(PdfPageWriter page)
    {
        ThrowIfClosed();
        ArgumentNullException.ThrowIfNull(page);
        if (!ReferenceEquals(page, _openPage))
        {
            throw new InvalidOperationException("That page isn't the open page of this document.");
        }

        // Finished first, so unfinished content throws before anything reaches the file.
        var content = page.Content.Finish();

        // Only the fonts this page used, each pointing at the document's one object for that font.
        var resources = new PdfDictionary();
        if (page.Content.FontsUsed.Count > 0)
        {
            var fonts = new PdfDictionary();
            foreach (var font in page.Content.FontsUsed)
            {
                fonts.Set(_fonts.NameOf(font), _fonts.ReferenceOf(font));
            }

            resources.Set(PdfName.Font, fonts);
        }

        var mediaBox = new PdfArray(new PdfInteger(0), new PdfInteger(0), new PdfReal(page.Width), new PdfReal(page.Height));
        var dictionary = new PdfDictionary()
            .Set(PdfName.Type, PdfName.Page)
            .Set(PdfName.Parent, _pageTree)
            .Set(PdfName.MediaBox, mediaBox)
            .Set(PdfName.Resources, resources);

        if (content.Length > 0)
        {
            dictionary.Set(PdfName.Contents, _file.Write(new PdfStream(new PdfDictionary(), content)));
        }

        _kids.Add(_file.Write(dictionary));
        page.Content.Dispose();
        _openPage = null;
    }

    /// <summary>Writes the page tree, the catalog, the cross-reference table and the trailer.</summary>
    /// <exception cref="InvalidOperationException">A page is still open, there are no pages, or the document is closed.</exception>
    public void Close()
    {
        ThrowIfClosed();
        if (_openPage is not null)
        {
            throw new InvalidOperationException("A page is still open. End it before closing the document.");
        }

        if (_kids.Count == 0)
        {
            throw new InvalidOperationException("A document needs at least one page.");
        }

        // Shared resources come first, once each (spec D-05).
        _fonts.WriteAll();

        _file.Write(_pageTree, new PdfDictionary()
            .Set(PdfName.Type, PdfName.Pages)
            .Set(PdfName.Kids, _kids)
            .Set(PdfName.Count, new PdfInteger(_kids.Count)));

        var catalog = _file.Write(new PdfDictionary()
            .Set(PdfName.Type, PdfName.Catalog)
            .Set(PdfName.Pages, _pageTree));

        _file.Close(catalog);
        _closed = true;
    }

    /// <summary>Returns pooled buffers. Disposing without <see cref="Close"/> leaves an incomplete file.</summary>
    public void Dispose()
    {
        _openPage?.Content.Dispose();
        _file.Dispose();
    }

    private static void ThrowIfNotPositive(double value, string paramName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "A page size must be a positive, finite number of points.");
        }
    }

    private void ThrowIfClosed()
    {
        if (_closed)
        {
            throw new InvalidOperationException("The document is closed.");
        }
    }
}
