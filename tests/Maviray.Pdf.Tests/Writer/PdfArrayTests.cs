using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

public sealed class PdfArrayTests
{
    [Fact]
    public void WritesAnEmptyArray() => Assert.Equal("[]", Serialized.Of(new PdfArray()));

    [Fact]
    public void SeparatesItemsWithSingleSpaces()
    {
        var array = new PdfArray(new PdfInteger(1), new PdfName("Name"), new PdfString("s"u8.ToArray()));

        Assert.Equal("[1 /Name (s)]", Serialized.Of(array));
    }

    [Fact]
    public void NestsArrays()
    {
        var array = new PdfArray(
            new PdfArray(new PdfInteger(1), new PdfInteger(2)),
            new PdfArray(new PdfInteger(3)));

        Assert.Equal("[[1 2] [3]]", Serialized.Of(array));
    }

    [Fact]
    public void AddAppendsAtTheEnd()
    {
        var array = new PdfArray(new PdfInteger(1));

        array.Add(new PdfInteger(2));

        Assert.Equal(2, array.Count);
        Assert.Equal("[1 2]", Serialized.Of(array));
    }
}
