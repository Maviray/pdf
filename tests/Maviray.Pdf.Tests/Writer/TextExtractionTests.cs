using System.IO.Compression;
using Maviray.Pdf.Tests.Validation;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

/// <summary>
/// Reads Helvetica text back with pdftotext, a reader we didn't write: the text must come out exactly as it went in
/// (spec D-10). Locally, Git Bash supplies xpdf's pdftotext; CI uses poppler's, which is the reference.
/// </summary>
public sealed class TextExtractionTests
{
    [Fact]
    public void PdftotextReadsAsciiTextBack()
    {
        var path = GeneratedFiles.Save(PageWithText("Hello, world"));

        Assert.Equal("Hello, world", Poppler.ExtractText(path));
    }

    [Fact]
    public void PdftotextReadsLatin1TextBack()
    {
        const string text = "Grüße aus Köln – 25 €"; // Grüße aus Köln – 25 €
        var path = GeneratedFiles.Save(PageWithText(text));

        Assert.Equal(text, Poppler.ExtractText(path));
    }

    [Fact]
    public void TextPagesPassQpdf()
    {
        Qpdf.Check(GeneratedFiles.Save(PageWithText("Hello, world")));
        Qpdf.Check(GeneratedFiles.Save(PageWithText("Grüße aus Köln – 25 €"), name: "Latin1TextPassesQpdf"));
    }

    private static byte[] PageWithText(string text) => PdfDocumentWriterTests.Write(
        document =>
        {
            var page = document.BeginPage(595.2756, 841.8898);
            page.Content.BeginText().SetFont(StandardFont.Helvetica, 24).SetTextMatrix(1, 0, 0, 1, 72, 750).ShowText(text).EndText();
            document.EndPage(page);
        },
        CompressionLevel.Optimal);
}
