using System.Text;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

public sealed class PdfOutputTests
{
    [Fact]
    public void WritesBytesAndCountsPosition()
    {
        var stream = new MemoryStream();
        using var output = new PdfOutput(stream);

        output.Write("%PDF"u8);
        output.WriteByte((byte)'\n');

        Assert.Equal(5, output.Position);
        output.Flush();
        Assert.Equal("%PDF\n"u8.ToArray(), stream.ToArray());
    }

    [Fact]
    public void ManySmallWritesCrossTheBufferEdgeInOrder()
    {
        var stream = new MemoryStream();
        using var output = new PdfOutput(stream, bufferSize: 16);
        var expected = Enumerable.Range(0, 40).Select(i => (byte)i).ToArray();

        foreach (var value in expected)
        {
            output.WriteByte(value);
        }

        output.Flush();
        Assert.Equal(expected, stream.ToArray());
        Assert.Equal(40, output.Position);
    }

    [Fact]
    public void MultiByteWritesCrossTheBufferEdgeInOrder()
    {
        var stream = new MemoryStream();
        using var output = new PdfOutput(stream, bufferSize: 16);
        var expected = Enumerable.Range(0, 100).Select(i => (byte)i).ToArray();

        for (var offset = 0; offset < expected.Length; offset += 5)
        {
            output.Write(expected.AsSpan(offset, 5)); // 5 doesn't divide 16, so spans straddle the edge
        }

        output.Flush();
        Assert.Equal(expected, stream.ToArray());
        Assert.Equal(100, output.Position);
    }

    [Fact]
    public void OneWriteLargerThanTheBufferArrivesWhole()
    {
        var stream = new MemoryStream();
        using var output = new PdfOutput(stream, bufferSize: 16);
        var expected = Enumerable.Range(0, 200_000).Select(i => (byte)(i % 251)).ToArray();

        output.WriteByte(expected[0]);
        output.Write(expected.AsSpan(1));

        output.Flush();
        Assert.Equal(expected, stream.ToArray());
        Assert.Equal(200_000, output.Position);
    }

    [Fact]
    public void WorksWithAStreamThatCannotSeek()
    {
        using var stream = new NonSeekableStream();
        using var output = new PdfOutput(stream);
        var expected = Enumerable.Range(0, 10).Select(i => (byte)i).ToArray();

        output.Write(expected);
        output.Flush();

        Assert.Equal(10, output.Position);
        Assert.Equal(expected, stream.Inner.ToArray());
    }

    [Fact]
    public void DisposeLeavesTheDestinationOpen()
    {
        var stream = new MemoryStream();
        var output = new PdfOutput(stream);

        output.Dispose();

        Assert.True(stream.CanWrite);
    }

    [Fact]
    public void WritingAfterDisposeThrows()
    {
        var output = new PdfOutput(new MemoryStream());
        output.Dispose();

        Assert.Throws<ObjectDisposedException>(() => output.Write("a"u8));
        Assert.Throws<ObjectDisposedException>(() => output.WriteByte(0));
        Assert.Throws<ObjectDisposedException>(() => output.WriteInteger(1));
        Assert.Throws<ObjectDisposedException>(output.Flush);
    }

    [Theory]
    [InlineData(0L, "0")]
    [InlineData(42L, "42")]
    [InlineData(-7L, "-7")]
    [InlineData(long.MinValue, "-9223372036854775808")]
    [InlineData(long.MaxValue, "9223372036854775807")]
    public void WritesIntegersAsInvariantDigits(long value, string expected)
    {
        var stream = new MemoryStream();
        using var output = new PdfOutput(stream);

        output.WriteInteger(value);
        output.Flush();

        Assert.Equal(expected, Encoding.Latin1.GetString(stream.ToArray()));
    }
}
