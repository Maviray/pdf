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
    /// predictable; the hash-based ID of deterministic mode comes in P1.
    /// </summary>
    public byte[]? FileId { get; init; }
}
