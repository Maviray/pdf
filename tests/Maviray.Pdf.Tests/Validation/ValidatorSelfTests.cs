using System.IO.Compression;
using System.Text;
using Maviray.Pdf.Tests.Writer;
using Xunit.Sdk;

namespace Maviray.Pdf.Tests.Validation;

/// <summary>
/// Proves that each validator helper can fail. Each test locates its tool first, so a missing tool skips the test
/// instead of letting the skip satisfy the assertion.
/// </summary>
public sealed class ValidatorSelfTests
{
    [Fact]
    public void QpdfRejectsAFileWithAWrongStartxref()
    {
        ExternalTool.Locate("qpdf");
        var text = Encoding.Latin1.GetString(BlankPage());
        var keyword = text.LastIndexOf("startxref\n", StringComparison.Ordinal) + "startxref\n".Length;
        var path = GeneratedFiles.Save(Encoding.Latin1.GetBytes(text[..keyword] + "0\n%%EOF\n"));

        Assert.ThrowsAny<XunitException>(() => Qpdf.Check(path));
    }

    [Fact]
    public void PopplerRejectsAFileThatIsNotAPdf()
    {
        ExternalTool.Locate("pdftoppm");
        var path = GeneratedFiles.Save("not a pdf"u8.ToArray());

        Assert.ThrowsAny<XunitException>(() => Poppler.Render(path));
    }

    [Fact]
    public void ExtractTextReadsNothingFromABlankPage()
    {
        var path = GeneratedFiles.Save(BlankPage());

        Assert.Equal("", Poppler.ExtractText(path));
    }

    private static byte[] BlankPage() =>
        PdfDocumentWriterTests.Write(document => document.EndPage(document.BeginPage(595.2756, 841.8898)), CompressionLevel.Optimal);
}
