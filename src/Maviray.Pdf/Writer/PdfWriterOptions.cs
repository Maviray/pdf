using System.IO.Compression;

namespace Maviray.Pdf.Writer;

/// <summary>Settings for one file written by <see cref="PdfFileWriter"/>.</summary>
internal sealed class PdfWriterOptions
{
    /// <summary>
    /// How streams are compressed (spec D-08). <see cref="CompressionLevel.NoCompression"/> writes stream data as it is,
    /// with no filter, so a content stream can be read in the file.
    /// </summary>
    public CompressionLevel CompressionLevel { get; init; } = CompressionLevel.Optimal;

    /// <summary>
    /// The 16 bytes of the trailer's <c>/ID</c>, or null for random bytes (spec D-07). Tests set it so the output is
    /// predictable.
    /// </summary>
    public byte[]? FileId { get; init; }

    /// <summary>
    /// Whether the <c>/ID</c> is the first 16 bytes of the SHA-256 of every byte written before the trailer, which
    /// deterministic mode uses (L-13). It can't be combined with <see cref="FileId"/>.
    /// </summary>
    public bool HashedFileId { get; init; }

    /// <summary>The document information dictionary's values (L-12), or null for <c>/Producer</c> alone.</summary>
    public PdfDocumentInfo? Info { get; init; }

    /// <summary>The catalog's <c>/Lang</c>, a BCP 47 tag the caller checked, or null for none (L-12).</summary>
    public string? Language { get; init; }
}
