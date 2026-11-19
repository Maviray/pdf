using System.Text;
using System.Text.RegularExpressions;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

public sealed class PdfFileWriterTests
{
    private static readonly byte[] FixedId = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();

    private static readonly Regex IdPattern = new(@"/ID \[<([0-9A-F]{32})> <([0-9A-F]{32})>\]", RegexOptions.CultureInvariant);

    [Fact]
    public void WritesTheHeaderFirst()
    {
        var text = Encoding.Latin1.GetString(WriteFile(writer => writer.Write(new PdfDictionary())));

        Assert.StartsWith("%PDF-1.7\n%âãÏÓ\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FramesEachIndirectObject()
    {
        var text = Encoding.Latin1.GetString(WriteFile(writer => writer.Write(new PdfDictionary())));

        Assert.StartsWith("1 0 obj\n<<>>\nendobj\n", text["%PDF-1.7\n%âãÏÓ\n".Length..], StringComparison.Ordinal);
    }

    [Fact]
    public void EveryOffsetPointsAtItsObject()
    {
        AssertOffsetsPointAtObjects(new MemoryStream(), stream => ((MemoryStream)stream).ToArray());
    }

    [Fact]
    public void OffsetsAreRightWithANonSeekableDestination()
    {
        AssertOffsetsPointAtObjects(new NonSeekableStream(), stream => ((NonSeekableStream)stream).Inner.ToArray());
    }

    [Fact]
    public void TrailerNamesSizeRootAndId()
    {
        var file = PdfFileStructure.Parse(WriteFile(writer => writer.Write(new PdfDictionary()), FixedId));

        Assert.Equal(
            "<</Size 2 /Root 1 0 R /ID [<000102030405060708090A0B0C0D0E0F> <000102030405060708090A0B0C0D0E0F>]>>",
            file.Trailer);
        Assert.EndsWith($"startxref\n{file.StartXref}\n%%EOF\n", file.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void RandomIdsDifferBetweenFiles()
    {
        var first = IdHalves(WriteFile(writer => writer.Write(new PdfDictionary())));
        var second = IdHalves(WriteFile(writer => writer.Write(new PdfDictionary())));

        Assert.Equal(first.Left, first.Right);
        Assert.Equal(second.Left, second.Right);
        Assert.NotEqual(first.Left, second.Left);
    }

    [Fact]
    public void RefusesAFileIdThatIsNot16Bytes()
    {
        var options = new PdfWriterOptions { FileId = new byte[15] };

        Assert.Throws<ArgumentException>(() => new PdfFileWriter(new MemoryStream(), options));
    }

    [Fact]
    public void WritingAnObjectTwiceThrows()
    {
        using var writer = new PdfFileWriter(new MemoryStream(), new PdfWriterOptions());
        var reference = writer.Write(new PdfDictionary());
        var position = writer.Position;

        Assert.Throws<InvalidOperationException>(() => writer.Write(reference, new PdfDictionary()));
        Assert.Equal(position, writer.Position);
    }

    [Fact]
    public void ClosingWithAnUnwrittenObjectNamesIt()
    {
        using var writer = new PdfFileWriter(new MemoryStream(), new PdfWriterOptions());
        var first = writer.Reserve();
        writer.Reserve();
        writer.Write(first, new PdfDictionary());

        var error = Assert.Throws<InvalidOperationException>(() => writer.Close(first));

        Assert.Contains("2", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesAReferenceItNeverReserved()
    {
        using var writer = new PdfFileWriter(new MemoryStream(), new PdfWriterOptions());
        writer.Reserve();

        Assert.Throws<InvalidOperationException>(() => writer.Write(new PdfReference(5), new PdfDictionary()));
    }

    [Fact]
    public void RefusesWritesAfterClose()
    {
        using var writer = new PdfFileWriter(new MemoryStream(), new PdfWriterOptions());
        var root = writer.Write(new PdfDictionary());
        writer.Close(root);

        Assert.Throws<InvalidOperationException>(writer.Reserve);
        Assert.Throws<InvalidOperationException>(() => writer.Write(new PdfDictionary()));
        Assert.Throws<InvalidOperationException>(() => writer.Close(root));
    }

    [Fact]
    public void CompressesStreamsWithFlate()
    {
        var data = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("BT ET\n", 1000)));

        var file = PdfFileStructure.Parse(WriteFile(writer => writer.Write(new PdfStream(new PdfDictionary(), data))));

        var obj = file.ObjectText(1);
        Assert.Contains("/Filter /FlateDecode", obj, StringComparison.Ordinal);
        var encoded = StreamBytes(obj);
        Assert.Contains($"/Length {encoded.Length} ", obj, StringComparison.Ordinal);
        Assert.Equal(data, Inflate(encoded));
    }

    [Fact]
    public void WritesStreamsAsTheyAreWithoutCompression()
    {
        var options = new PdfWriterOptions { CompressionLevel = System.IO.Compression.CompressionLevel.NoCompression };

        var file = PdfFileStructure.Parse(
            WriteFile(writer => writer.Write(new PdfStream(new PdfDictionary(), "0 0 m\n"u8.ToArray())), options: options));

        Assert.Equal("1 0 obj\n<</Length 6>>\nstream\n0 0 m\n\nendstream\nendobj\n", file.ObjectText(1));
    }

    [Fact]
    public void AFailedWriteFaultsTheWriter()
    {
        using var writer = new PdfFileWriter(new MemoryStream(), new PdfWriterOptions());
        var root = writer.Write(new PdfDictionary());
        var dictionary = new PdfDictionary();
        var stream = new PdfStream(dictionary, "data"u8.ToArray());
        dictionary.Set(PdfName.Length, new PdfInteger(4)); // the stream now refuses to be written
        Assert.Throws<InvalidOperationException>(() => writer.Write(stream));

        var error = Assert.Throws<InvalidOperationException>(() => writer.Close(root));

        Assert.Contains("incomplete", error.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(writer.Reserve);
    }

    [Fact]
    public void RefusesAnUndefinedCompressionLevel()
    {
        var options = new PdfWriterOptions { CompressionLevel = (System.IO.Compression.CompressionLevel)99 };

        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfFileWriter(new MemoryStream(), options));
    }

    [Fact]
    public void DisposeLeavesTheDestinationOpen()
    {
        var destination = new MemoryStream();
        var writer = new PdfFileWriter(destination, new PdfWriterOptions());

        writer.Dispose();

        Assert.True(destination.CanWrite);
    }

    /// <summary>Writes a file whose root is the first object <paramref name="body"/> writes.</summary>
    internal static byte[] WriteFile(Func<PdfFileWriter, PdfReference> body, byte[]? fileId = null, PdfWriterOptions? options = null)
    {
        var destination = new MemoryStream();
        using (var writer = new PdfFileWriter(destination, options ?? new PdfWriterOptions { FileId = fileId }))
        {
            var root = body(writer);
            writer.Close(root);
        }

        return destination.ToArray();
    }

    private static void AssertOffsetsPointAtObjects(Stream destination, Func<Stream, byte[]> bytesOf)
    {
        using (var writer = new PdfFileWriter(destination, new PdfWriterOptions()))
        {
            var first = writer.Reserve();
            var second = writer.Reserve();
            var third = writer.Reserve();
            writer.Write(second, new PdfInteger(2));
            writer.Write(first, new PdfInteger(1));
            writer.Write(third, new PdfInteger(3));
            writer.Close(first);
        }

        var file = PdfFileStructure.Parse(bytesOf(destination));

        for (var number = 1; number <= 3; number++)
        {
            Assert.StartsWith($"{number} 0 obj\n", file.Text[checked((int)file.Offsets[number])..], StringComparison.Ordinal);
        }

        Assert.StartsWith("xref\n0 4\n", file.Text[checked((int)file.StartXref)..], StringComparison.Ordinal);
    }

    private static byte[] StreamBytes(string objectText)
    {
        var begin = objectText.IndexOf(">>\nstream\n", StringComparison.Ordinal) + ">>\nstream\n".Length;
        var end = objectText.LastIndexOf("\nendstream", StringComparison.Ordinal);
        return Encoding.Latin1.GetBytes(objectText[begin..end]);
    }

    private static byte[] Inflate(byte[] compressed)
    {
        using var inflater = new System.IO.Compression.ZLibStream(new MemoryStream(compressed), System.IO.Compression.CompressionMode.Decompress);
        var result = new MemoryStream();
        inflater.CopyTo(result);
        return result.ToArray();
    }

    private static (string Left, string Right) IdHalves(byte[] bytes)
    {
        var match = IdPattern.Match(PdfFileStructure.Parse(bytes).Trailer);
        Assert.True(match.Success, "The trailer has no /ID of two 16-byte hex strings.");
        return (match.Groups[1].Value, match.Groups[2].Value);
    }
}
