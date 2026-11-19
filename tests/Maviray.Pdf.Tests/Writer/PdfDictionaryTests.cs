using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

public sealed class PdfDictionaryTests
{
    private static readonly PdfName A = new("A");
    private static readonly PdfName B = new("B");

    [Fact]
    public void WritesAnEmptyDictionary() => Assert.Equal("<<>>", Serialized.Of(new PdfDictionary()));

    [Fact]
    public void WritesEntriesInInsertionOrder()
    {
        var dictionary = new PdfDictionary()
            .Set(new PdfName("Type"), new PdfName("Page"))
            .Set(new PdfName("Count"), new PdfInteger(1));

        Assert.Equal("<</Type /Page /Count 1>>", Serialized.Of(dictionary));
    }

    [Fact]
    public void SettingAnExistingKeyReplacesItInPlace()
    {
        var dictionary = new PdfDictionary()
            .Set(A, new PdfInteger(1))
            .Set(B, new PdfInteger(2))
            .Set(new PdfName("A"), new PdfInteger(3));

        Assert.Equal("<</A 3 /B 2>>", Serialized.Of(dictionary));
        Assert.Equal(2, dictionary.Count);
    }

    [Fact]
    public void RefusesANullValue()
    {
        var dictionary = new PdfDictionary();

        Assert.Throws<ArgumentException>(() => dictionary.Set(A, PdfNull.Instance));
    }

    [Fact]
    public void RemovesAndLooksUpKeys()
    {
        var dictionary = new PdfDictionary().Set(A, new PdfInteger(1));
        Assert.True(dictionary.ContainsKey(A));
        Assert.True(dictionary.TryGetValue(A, out var found));
        Assert.Equal("1", Serialized.Of(found));

        Assert.True(dictionary.Remove(A));

        Assert.False(dictionary.Remove(A));
        Assert.False(dictionary.ContainsKey(A));
        Assert.False(dictionary.TryGetValue(A, out _));
        Assert.Equal("<<>>", Serialized.Of(dictionary));
    }

    [Fact]
    public void NestsDictionaries()
    {
        var dictionary = new PdfDictionary().Set(A, new PdfDictionary().Set(B, new PdfInteger(1)));

        Assert.Equal("<</A <</B 1>>>>", Serialized.Of(dictionary));
    }
}
