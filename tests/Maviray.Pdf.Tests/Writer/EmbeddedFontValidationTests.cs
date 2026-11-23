using System.IO.Compression;
using Maviray.Pdf.Fonts;
using Maviray.Pdf.Tests.Fonts;
using Maviray.Pdf.Tests.Validation;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

/// <summary>
/// Embedded fonts checked by tools we didn't write (spec F-13): qpdf accepts the file, pdffonts reports the font as an
/// embedded, subset, Unicode-mapped CID TrueType font, and pdftotext returns the text exactly, including a character
/// the font lacks.
/// </summary>
public sealed class EmbeddedFontValidationTests
{
    [Fact]
    public void ALineOfNotoSansPassesQpdfPdffontsAndPdftotext()
    {
        const string line = "Grüße aus Köln: Äpfel, Öl und Übermaß";
        var path = GeneratedFiles.Save(OneLine(TestFonts.NotoSans("Regular"), line));

        Qpdf.Check(path);
        var font = Assert.Single(Poppler.ListFonts(path));
        Assert.Equal("CID TrueType", font.Type);
        Assert.True(font.Embedded, "pdffonts reports the font as not embedded.");
        Assert.True(font.Subset, "pdffonts reports the font as not subset.");
        Assert.True(font.Unicode, "pdffonts reports the font as not Unicode-mapped.");
        Assert.Matches(@"^[A-Z]{6}\+NotoSans-Regular$", font.Name);
        Assert.Equal(line, Poppler.ExtractText(path));
    }

    [Fact]
    public void ACharacterTheFontLacksStillCopiesOut()
    {
        const string line = "Köln 中"; // U+4E2D has no glyph in Noto Sans, so it is drawn as .notdef
        var path = GeneratedFiles.Save(OneLine(TestFonts.NotoSans("Regular"), line));

        Assert.Equal(line, Poppler.ExtractText(path));
    }

    [Fact]
    public void AWholeFontIsEmbeddedButNotSubset()
    {
        var path = GeneratedFiles.Save(OneLine(TrueTypeFont.Load(new TestFontBuilder().FsType(0x0100).Build()), "AB A"));

        Qpdf.Check(path);
        var font = Assert.Single(Poppler.ListFonts(path));
        Assert.Equal("TestFont-Regular", font.Name);
        Assert.True(font.Embedded, "pdffonts reports the font as not embedded.");
        Assert.False(font.Subset, "pdffonts reports the whole font as a subset.");
        Assert.True(font.Unicode, "pdffonts reports the font as not Unicode-mapped.");
        Assert.Equal("AB A", Poppler.ExtractText(path));
    }

    private static byte[] OneLine(TrueTypeFont font, string text) => PdfDocumentWriterTests.Write(
        document =>
        {
            var embedded = document.EmbedFont(font);
            var page = document.BeginPage(595.2756, 841.8898);
            page.Content.BeginText().SetFont(embedded, 24).SetTextMatrix(1, 0, 0, 1, 72, 750)
                .ShowGlyphs(EmbeddedText.Encode(embedded, text)).EndText();
            document.EndPage(page);
        },
        CompressionLevel.Optimal);
}
