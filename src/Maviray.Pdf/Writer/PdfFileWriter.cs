using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Maviray.Pdf.Writer;

/// <summary>
/// Writes the structure of one PDF file (spec D-05): the header, numbered indirect objects as they are completed, the
/// cross-reference table and the trailer. Only the offset of each object is kept, so memory doesn't grow with the
/// objects' size. The caller owns the destination stream: the writer never disposes it.
/// </summary>
internal sealed class PdfFileWriter : IDisposable
{
    private const long MaxOffset = 9_999_999_999; // Ten digits: the widest offset an xref entry can hold.

    private readonly PdfOutput _output;
    private readonly PdfWriterOptions _options;
    private readonly byte[] _fileId;

    // The byte offset of each object, indexed by object number; -1 means reserved but not yet written.
    // Index 0 stands for the free entry and is never written.
    private readonly List<long> _offsets = [-1];
    private bool _closed;

    /// <exception cref="ArgumentException"><see cref="PdfWriterOptions.FileId"/> is set but isn't 16 bytes long.</exception>
    public PdfFileWriter(Stream destination, PdfWriterOptions options)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(options);
        if (options.FileId is { Length: not 16 })
        {
            throw new ArgumentException("The file ID must be exactly 16 bytes.", nameof(options));
        }

        _options = options;
        _fileId = options.FileId is null ? RandomNumberGenerator.GetBytes(16) : [.. options.FileId];
        _output = new PdfOutput(destination);

        // The second line is a comment of four bytes above 127, so transfer tools treat the file as binary.
        _output.Write("%PDF-1.7\n%"u8);
        _output.Write([0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n']);
    }

    /// <summary>The number of bytes written so far.</summary>
    public long Position => _output.Position;

    /// <summary>Hands out the next object number, so other objects can refer to it before it is written.</summary>
    public PdfReference Reserve()
    {
        ThrowIfClosed();
        _offsets.Add(-1);
        return new PdfReference(_offsets.Count - 1);
    }

    /// <summary>Reserves the next object number and writes <paramref name="value"/> under it.</summary>
    public PdfReference Write(PdfObject value)
    {
        var reference = Reserve();
        Write(reference, value);
        return reference;
    }

    /// <summary>Writes <paramref name="value"/> as the indirect object <paramref name="reference"/>.</summary>
    /// <exception cref="InvalidOperationException">The reference wasn't reserved by this writer, its object has already
    /// been written, or the file is closed.</exception>
    public void Write(PdfReference reference, PdfObject value)
    {
        ThrowIfClosed();
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(value);

        var number = reference.ObjectNumber;
        if (number >= _offsets.Count)
        {
            throw new InvalidOperationException($"Object {number} was never reserved by this writer.");
        }

        if (_offsets[number] >= 0)
        {
            throw new InvalidOperationException($"Object {number} has already been written.");
        }

        _offsets[number] = _output.Position;
        _output.WriteInteger(number);
        _output.Write(" 0 obj\n"u8);
        if (value is PdfStream stream && _options.CompressionLevel != CompressionLevel.NoCompression)
        {
            WriteCompressed(stream);
        }
        else
        {
            value.WriteTo(_output);
        }

        _output.Write("\nendobj\n"u8);
    }

    /// <summary>
    /// Writes the cross-reference table and the trailer with <paramref name="root"/> as the catalog, then flushes the
    /// output and the destination. Nothing can be written afterwards.
    /// </summary>
    /// <exception cref="InvalidOperationException">A reserved object was never written, or the file is already closed.</exception>
    public void Close(PdfReference root)
    {
        ThrowIfClosed();
        ArgumentNullException.ThrowIfNull(root);

        var unwritten = Enumerable.Range(1, _offsets.Count - 1).Where(number => _offsets[number] < 0).ToList();
        if (unwritten.Count > 0)
        {
            throw new InvalidOperationException(
                $"Objects {string.Join(", ", unwritten)} were reserved but never written.");
        }

        if (root.ObjectNumber >= _offsets.Count)
        {
            throw new InvalidOperationException($"The root, object {root.ObjectNumber}, was never written by this writer.");
        }

        var xref = _output.Position;
        _output.Write("xref\n0 "u8);
        _output.WriteInteger(_offsets.Count);
        _output.Write("\n0000000000 65535 f \n"u8);
        for (var number = 1; number < _offsets.Count; number++)
        {
            WriteOffset(_offsets[number]);
            _output.Write(" 00000 n \n"u8);
        }

        var id = new PdfString(_fileId, PdfStringForm.Hex);
        var trailer = new PdfDictionary()
            .Set(PdfName.Size, new PdfInteger(_offsets.Count))
            .Set(PdfName.Root, root)
            .Set(PdfName.Id, new PdfArray(id, id));

        _output.Write("trailer\n"u8);
        trailer.WriteTo(_output);
        _output.Write("\nstartxref\n"u8);
        _output.WriteInteger(xref);
        _output.Write("\n%%EOF\n"u8);
        _output.Flush();
        _closed = true;
    }

    /// <summary>Returns the output's buffer. Disposing without <see cref="Close"/> leaves an incomplete file.</summary>
    public void Dispose() => _output.Dispose();

    // Flate is zlib (RFC 1950) data, which is what ZLibStream writes (spec D-08).
    private void WriteCompressed(PdfStream stream)
    {
        using var compressed = new MemoryStream();
        using (var deflater = new ZLibStream(compressed, _options.CompressionLevel, leaveOpen: true))
        {
            deflater.Write(stream.Data.Span);
        }

        stream.WriteEncoded(_output, compressed.GetBuffer().AsSpan(0, checked((int)compressed.Length)), PdfName.FlateDecode);
    }

    private void WriteOffset(long offset)
    {
        if (offset > MaxOffset)
        {
            throw new InvalidOperationException("The file is larger than a cross-reference table can address (10 GB).");
        }

        Span<byte> digits = stackalloc byte[10];
        offset.TryFormat(digits, out _, "D10", CultureInfo.InvariantCulture);
        _output.Write(digits);
    }

    private void ThrowIfClosed()
    {
        if (_closed)
        {
            throw new InvalidOperationException("The file is closed.");
        }
    }
}
