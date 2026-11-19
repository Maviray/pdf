using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

public sealed class StandardFontTests
{
    // No /Widths and no font descriptor: PDF 1.7 allows that for the standard 14 fonts (spec D-10).
    [Fact]
    public void HelveticaDictionary()
    {
        Assert.Equal(
            "<</Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding>>",
            Serialized.Of(StandardFont.Helvetica.CreateDictionary()));
    }
}
