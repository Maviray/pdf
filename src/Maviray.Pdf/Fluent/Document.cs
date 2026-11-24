using Maviray.Pdf.Drawing;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf;

/// <summary>
/// A PDF document, described by a compose callback. Each save runs the callback again and generates the document, so a
/// document can be saved many times, and from many threads.
/// </summary>
public sealed class Document
{
    private readonly Action<DocumentBuilder> _compose;

    private Document(Action<DocumentBuilder> compose) => _compose = compose;

    /// <summary>A document described by <paramref name="compose"/>, which adds its settings, metadata and page sets.</summary>
    /// <param name="compose">Builds the document; it runs again on every save.</param>
    /// <exception cref="ArgumentNullException"><paramref name="compose"/> is null.</exception>
    public static Document Create(Action<DocumentBuilder> compose)
    {
        ArgumentNullException.ThrowIfNull(compose);
        return new Document(compose);
    }

    /// <summary>Writes the document to <paramref name="output"/>, which stays open.</summary>
    /// <param name="output">A writable stream; it needn't seek.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The document has no page sets, or a builder was misused.</exception>
    /// <exception cref="LayoutException">Content can't be laid out. Whatever was written before stays in the stream.</exception>
    public void Save(Stream output)
    {
        ArgumentNullException.ThrowIfNull(output);
        Generate(output);
    }

    /// <summary>
    /// Writes the document to the file at <paramref name="path"/>: first to a temporary file beside it, which is moved
    /// into place when generation succeeds. A failure leaves any existing file unchanged and deletes the temporary file,
    /// unless another program holds it.
    /// </summary>
    /// <param name="path">The file's path.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">The document has no page sets, or a builder was misused.</exception>
    /// <exception cref="LayoutException">Content can't be laid out.</exception>
    /// <exception cref="IOException">The file can't be written or moved.</exception>
    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var target = Path.GetFullPath(path);
        var temporary = $"{target}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                Generate(stream);
            }

            File.Move(temporary, target, overwrite: true);
        }
        catch
        {
            DeleteQuietly(temporary);
            throw;
        }
    }

    /// <summary>The document as the bytes of a PDF file.</summary>
    /// <exception cref="InvalidOperationException">The document has no page sets, or a builder was misused.</exception>
    /// <exception cref="LayoutException">Content can't be laid out.</exception>
    public byte[] ToBytes()
    {
        using var stream = new MemoryStream();
        Generate(stream);
        return stream.ToArray();
    }

    // The cleanup after a failed save. Its own failure, such as another program holding the file, must not hide the
    // error that made the save fail.
    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void Generate(Stream output)
    {
        var started = DateTimeOffset.Now;
        var builder = new DocumentBuilder();
        _compose(builder);
        if (builder.Pages.Count == 0)
        {
            throw new InvalidOperationException("A document needs at least one page set: call Page in the compose callback.");
        }

        var settings = builder.SettingsBuilder;
        var metadata = builder.MetadataBuilder;
        var sets = builder.Pages.Select(page => page.Build(settings.Registry)).ToList();
        var options = new PdfWriterOptions
        {
            CompressionLevel = settings.CompressionLevel,
            HashedFileId = settings.IsDeterministic,
            Language = metadata.LanguageTag,
            Info = new PdfDocumentInfo(
                metadata.TitleText,
                metadata.AuthorText,
                metadata.SubjectText,
                metadata.KeywordsText,
                metadata.CreatorText,
                metadata.Date ?? (settings.IsDeterministic ? null : started)),
        };

        using var writer = new PdfDocumentWriter(output, options);
        LayoutEngine.Generate(sets, new PdfCanvas(writer), new DiagnosticSink(settings.Handler));
        writer.Close();
    }
}
