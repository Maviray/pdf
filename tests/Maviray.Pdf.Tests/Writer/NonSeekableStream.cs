namespace Maviray.Pdf.Tests.Writer;

/// <summary>
/// A write-only stream that can't seek and doesn't know its position or length, like an HTTP response body.
/// Everything written goes to <see cref="Inner"/>, so a test can check the bytes.
/// </summary>
public sealed class NonSeekableStream : Stream
{
    public MemoryStream Inner { get; } = new();

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => Inner.Flush();

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => Inner.Write(buffer, offset, count);

    public override void Write(ReadOnlySpan<byte> buffer) => Inner.Write(buffer);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
