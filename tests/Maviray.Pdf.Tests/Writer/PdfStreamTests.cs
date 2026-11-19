using System.Text;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

public sealed class PdfStreamTests
{
    [Fact]
    public void WritesDictionaryLengthAndData()
    {
        var dictionary = new PdfDictionary().Set(new PdfName("Type"), new PdfName("XObject"));

        var text = Serialized.Of(new PdfStream(dictionary, "hello"u8.ToArray()));

        Assert.Equal("<</Type /XObject /Length 5>>\nstream\nhello\nendstream", text);
    }

    [Fact]
    public void WriteEncodedAddsTheFilterAfterTheLength()
    {
        var dictionary = new PdfDictionary().Set(new PdfName("Type"), new PdfName("XObject"));
        var stream = new PdfStream(dictionary, "raw data"u8.ToArray());
        var file = new MemoryStream();
        using (var output = new PdfOutput(file))
        {
            stream.WriteEncoded(output, "abc"u8, PdfName.FlateDecode);
            output.Flush();
        }

        Assert.Equal(
            "<</Type /XObject /Length 3 /Filter /FlateDecode>>\nstream\nabc\nendstream",
            Encoding.Latin1.GetString(file.ToArray()));
    }

    [Fact]
    public void WritesAnEmptyStream()
    {
        var text = Serialized.Of(new PdfStream(new PdfDictionary(), ReadOnlyMemory<byte>.Empty));

        Assert.Equal("<</Length 0>>\nstream\n\nendstream", text);
    }

    [Fact]
    public void KeepsBinaryDataByteForByte()
    {
        var data = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();

        var text = Serialized.Of(new PdfStream(new PdfDictionary(), data));

        var begin = text.IndexOf("stream\n", StringComparison.Ordinal) + "stream\n".Length;
        var end = text.LastIndexOf("\nendstream", StringComparison.Ordinal);
        Assert.Equal(Encoding.Latin1.GetString(data), text[begin..end]);
    }

    [Fact]
    public void LeavesTheCallersDictionaryUnchanged()
    {
        var dictionary = new PdfDictionary();
        var stream = new PdfStream(dictionary, "hello"u8.ToArray());

        _ = Serialized.Of(stream);

        Assert.False(dictionary.ContainsKey(PdfName.Length));
        Assert.Equal(0, dictionary.Count);
    }

    [Theory]
    [InlineData("Length")]
    [InlineData("Filter")]
    public void RefusesToWriteWhenLengthOrFilterIsSetLater(string key)
    {
        var dictionary = new PdfDictionary();
        var stream = new PdfStream(dictionary, "hello"u8.ToArray());
        dictionary.Set(new PdfName(key), new PdfInteger(5));

        Assert.Throws<InvalidOperationException>(() => Serialized.Of(stream));
    }

    [Theory]
    [InlineData("Length")]
    [InlineData("Filter")]
    public void RefusesADictionaryThatSetsLengthOrFilter(string key)
    {
        var dictionary = new PdfDictionary().Set(new PdfName(key), new PdfInteger(5));

        Assert.Throws<ArgumentException>(() => new PdfStream(dictionary, "hello"u8.ToArray()));
    }
}
