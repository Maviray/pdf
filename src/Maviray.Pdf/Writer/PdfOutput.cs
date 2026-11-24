using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;

namespace Maviray.Pdf.Writer;

/// <summary>
/// Buffered output for the PDF writer. It counts every byte it writes, so object offsets are known without seeking
/// or reading the destination's position, and the destination can be a stream that can't seek. The caller owns
/// the destination: this class never disposes it.
/// </summary>
internal sealed class PdfOutput : IDisposable
{
    private readonly Stream _destination;
    private readonly IncrementalHash? _hash;
    private byte[]? _buffer;
    private int _buffered;

    /// <param name="destination">The stream the bytes go to.</param>
    /// <param name="bufferSize">The buffer's size in bytes.</param>
    /// <param name="hash">Whether to keep a SHA-256 of every byte, for <see cref="HashSoFar"/>.</param>
    public PdfOutput(Stream destination, int bufferSize = 65536, bool hash = false)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfLessThan(bufferSize, 1);

        _destination = destination;
        _buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        _hash = hash ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256) : null;
    }

    private static ReadOnlySpan<byte> HexDigits => "0123456789ABCDEF"u8;

    /// <summary>The number of bytes written so far, whether or not they have reached the destination yet.</summary>
    public long Position { get; private set; }

    public void WriteByte(byte value)
    {
        var buffer = CurrentBuffer();
        if (_buffered == buffer.Length)
        {
            WriteBuffered(buffer);
        }

        buffer[_buffered++] = value;
        Position++;
    }

    public void Write(ReadOnlySpan<byte> bytes)
    {
        var buffer = CurrentBuffer();
        if (bytes.Length > buffer.Length - _buffered)
        {
            WriteBuffered(buffer);
        }

        if (bytes.Length > buffer.Length)
        {
            _hash?.AppendData(bytes);
            _destination.Write(bytes);
        }
        else
        {
            bytes.CopyTo(buffer.AsSpan(_buffered));
            _buffered += bytes.Length;
        }

        Position += bytes.Length;
    }

    public void WriteInteger(long value)
    {
        // The longest value is long.MinValue: a sign and 19 digits.
        Span<byte> digits = stackalloc byte[20];
        value.TryFormat(digits, out var length, default, CultureInfo.InvariantCulture);
        Write(digits[..length]);
    }

    /// <summary>Writes a byte as two uppercase hex digits.</summary>
    public void WriteHex(byte value)
    {
        WriteByte(HexDigits[value >> 4]);
        WriteByte(HexDigits[value & 0xF]);
    }

    /// <summary>Writes a real number in the format of <see cref="PdfNumberFormat"/>.</summary>
    public void WriteNumber(double value)
    {
        Span<byte> digits = stackalloc byte[PdfNumberFormat.MaxLength];
        var length = PdfNumberFormat.Format(value, digits);
        Write(digits[..length]);
    }

    /// <summary>Writes the buffered bytes to the destination and flushes it.</summary>
    public void Flush()
    {
        WriteBuffered(CurrentBuffer());
        _destination.Flush();
    }

    /// <summary>The SHA-256 of every byte written so far, which first go to the destination.</summary>
    /// <exception cref="InvalidOperationException">The output wasn't made to hash.</exception>
    public byte[] HashSoFar()
    {
        if (_hash is null)
        {
            throw new InvalidOperationException("This output doesn't keep a hash.");
        }

        WriteBuffered(CurrentBuffer());
        return _hash.GetCurrentHash();
    }

    /// <summary>Returns the buffer to the pool. Bytes that were never flushed are dropped.</summary>
    public void Dispose()
    {
        _hash?.Dispose();
        if (_buffer is not null)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = null;
        }
    }

    private byte[] CurrentBuffer()
    {
        ObjectDisposedException.ThrowIf(_buffer is null, this);
        return _buffer;
    }

    private void WriteBuffered(byte[] buffer)
    {
        if (_buffered > 0)
        {
            _hash?.AppendData(buffer, 0, _buffered);
            _destination.Write(buffer, 0, _buffered);
            _buffered = 0;
        }
    }
}
