using System.IO.Compression;
using System.Text;
using Maviray.Pdf.Fonts;
using Maviray.Pdf.Tests.Fonts;
using Maviray.Pdf.Tests.Writer;
using Maviray.Pdf.Writer;
using Xunit.Sdk;

namespace Maviray.Pdf.Tests.Validation;

/// <summary>
/// Proves that each validator helper can fail. Each test locates its tool first, so a missing tool skips the test
/// instead of letting the skip satisfy the assertion.
/// </summary>
public sealed class ValidatorSelfTests
{
    [Fact]
    public void FontToolsRejectsAFileThatIsNotAFont()
    {
        FontTools.Python();
        var path = GeneratedFiles.SaveAs(new byte[100], ".ttf");

        Assert.ThrowsAny<XunitException>(() => FontTools.Dump(path));
    }

    [Fact]
    public void FontToolsRejectsASubsetWithTheWrongGlyphs()
    {
        FontTools.Python();
        var font = new TestFontBuilder().Build();
        var original = GeneratedFiles.SaveAs(font, "-original.ttf");
        var subset = GeneratedFiles.SaveAs(FontSubsetter.Build(TrueTypeFont.Load(font), [0, 2, 3]).Data.ToArray(), "-subset.ttf");

        Assert.ThrowsAny<XunitException>(() => FontTools.CheckSubset(original, subset, [0, 3, 2]));
    }

    [Fact]
    public void FontToolsRejectsASubsetWithABadChecksum()
    {
        FontTools.Python();
        var font = new TestFontBuilder().Build();
        var bytes = FontSubsetter.Build(TrueTypeFont.Load(font), [0, 2, 3]).Data.ToArray();
        bytes[new TestFontFile(bytes).OffsetOf("glyf") + 12] ^= 0xFF;
        var original = GeneratedFiles.SaveAs(font, "-original.ttf");
        var subset = GeneratedFiles.SaveAs(bytes, "-subset.ttf");

        Assert.ThrowsAny<XunitException>(() => FontTools.CheckSubset(original, subset, [0, 2, 3]));
    }

    [Fact]
    public void QpdfRejectsAFileWithAWrongStartxref()
    {
        ExternalTool.Locate("qpdf");
        var text = Encoding.Latin1.GetString(BlankPage());
        var keyword = text.LastIndexOf("startxref\n", StringComparison.Ordinal) + "startxref\n".Length;
        var path = GeneratedFiles.Save(Encoding.Latin1.GetBytes(text[..keyword] + "0\n%%EOF\n"));

        Assert.ThrowsAny<XunitException>(() => Qpdf.Check(path));
    }

    // A content stream with an unknown operator: poppler renders the page and exits with 0, but reports a syntax error on
    // stderr. Only the stderr check can fail this file, so "exited with 0" in the message proves that check caught it.
    // (A wrong startxref doesn't work here: poppler 26.01 repairs it without a word.)
    [Fact]
    public void PopplerRejectsAFileItCanRenderButComplainsAbout()
    {
        ExternalTool.Locate("pdftoppm");
        var path = GeneratedFiles.Save(PageWithContent("xyz\n"u8.ToArray()));

        var error = Assert.ThrowsAny<XunitException>(() => Poppler.Render(path));

        Assert.Contains("exited with 0", error.Message, StringComparison.Ordinal);
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

    // Built with the file writer directly, because the document writer gets content streams only in P0.3.
    private static byte[] PageWithContent(byte[] content)
    {
        var destination = new MemoryStream();
        var options = new PdfWriterOptions { CompressionLevel = CompressionLevel.NoCompression };
        using (var file = new PdfFileWriter(destination, options))
        {
            var pages = file.Reserve();
            var contents = file.Write(new PdfStream(new PdfDictionary(), content));
            var page = file.Write(new PdfDictionary()
                .Set(PdfName.Type, PdfName.Page)
                .Set(PdfName.Parent, pages)
                .Set(PdfName.MediaBox, new PdfArray(new PdfInteger(0), new PdfInteger(0), new PdfInteger(100), new PdfInteger(100)))
                .Set(PdfName.Resources, new PdfDictionary())
                .Set(new PdfName("Contents"), contents));
            file.Write(pages, new PdfDictionary()
                .Set(PdfName.Type, PdfName.Pages)
                .Set(PdfName.Kids, new PdfArray(page))
                .Set(PdfName.Count, new PdfInteger(1)));
            file.Close(file.Write(new PdfDictionary().Set(PdfName.Type, PdfName.Catalog).Set(PdfName.Pages, pages)));
        }

        return destination.ToArray();
    }

    private static byte[] BlankPage() =>
        PdfDocumentWriterTests.Write(document => document.EndPage(document.BeginPage(595.2756, 841.8898)), CompressionLevel.Optimal);
}
