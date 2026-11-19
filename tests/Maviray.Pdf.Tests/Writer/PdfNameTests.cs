using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

public sealed class PdfNameTests
{
    [Theory]
    [InlineData("Type", "/Type")]
    [InlineData("!~", "/!~")] // 0x21 and 0x7E are the first and last regular characters
    [InlineData("A B", "/A#20B")]
    [InlineData("a#b", "/a#23b")]
    [InlineData("\u007F", "/#7F")]
    [InlineData("(", "/#28")]
    [InlineData(")", "/#29")]
    [InlineData("<", "/#3C")]
    [InlineData(">", "/#3E")]
    [InlineData("[", "/#5B")]
    [InlineData("]", "/#5D")]
    [InlineData("{", "/#7B")]
    [InlineData("}", "/#7D")]
    [InlineData("/", "/#2F")]
    [InlineData("%", "/#25")]
    [InlineData("Grüße", "/Gr#C3#BC#C3#9Fe")] // "Grüße": each UTF-8 byte is escaped
    public void WritesSlashAndEscapedBytes(string value, string expected)
    {
        Assert.Equal(expected, Serialized.Of(new PdfName(value)));
    }

    [Fact]
    public void RefusesAnEmptyName()
    {
        Assert.Throws<ArgumentException>(() => new PdfName(""));
    }

    [Fact]
    public void NamesWithTheSameValueAreEqual()
    {
        var name = new PdfName("Type");
        var same = new PdfName("Type");

        Assert.True(name.Equals(same));
        Assert.Equal(name.GetHashCode(), same.GetHashCode());
        Assert.False(name.Equals(new PdfName("type")));
    }

    [Fact]
    public void SharedNamesWriteTheirValue()
    {
        Assert.Equal("/Length", Serialized.Of(PdfName.Length));
        Assert.Equal("/Filter", Serialized.Of(PdfName.Filter));
    }
}
