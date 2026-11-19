using System.IO.Compression;
using Maviray.Pdf.Tests.Validation;

namespace Maviray.Pdf.Tests.Writer;

public sealed class BlankPageValidationTests
{
    [Fact]
    public void BlankA4PassesQpdfAndRendersWhite()
    {
        var path = GeneratedFiles.Save(PdfDocumentWriterTests.Write(
            document => document.EndPage(document.BeginPage(595.2756, 841.8898)),
            CompressionLevel.Optimal));

        Qpdf.Check(path);
        var page = Assert.Single(Poppler.Render(path));

        Assert.InRange(page.Width, 595, 596);
        Assert.InRange(page.Height, 841, 842);
        for (var y = 0; y < page.Height; y++)
        {
            for (var x = 0; x < page.Width; x++)
            {
                if (page.GetPixel(x, y) != (255, 255, 255))
                {
                    Assert.Fail($"Pixel ({x}, {y}) is {page.GetPixel(x, y)}, not white.");
                }
            }
        }
    }
}
