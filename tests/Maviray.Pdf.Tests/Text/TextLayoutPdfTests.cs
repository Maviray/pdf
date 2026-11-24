using System.IO.Compression;
using Maviray.Pdf.Fonts;
using Maviray.Pdf.Tests.Fonts;
using Maviray.Pdf.Tests.Validation;
using Maviray.Pdf.Tests.Writer;
using Maviray.Pdf.Text;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Text;

// P1.7 is done when a paragraph's layout draws on a PDF page that passes qpdf (spec, Work items).
public sealed class TextLayoutPdfTests
{
    // German, with the characters beyond ASCII written as {..} so the source stays ASCII.
    private const string Prose =
        "Die Rechnung f{ue}r den Monat November enth{ae}lt alle Leistungen, die wir f{ue}r Sie erbracht haben. " +
        "Bitte {ue}berweisen Sie den Betrag von 1.234,56{nbsp}{euro} bis zum 30. Dezember auf das angegebene Konto. ";

    private const string Underlined = "Bei Fragen zur Rechnung erreichen Sie uns jederzeit per E-Mail. ";

    private const string Spaced = "Mit freundlichen Gr{ue}{ss}en aus K{oe}ln, Ihr Team der Rechnungs{shy}abteilung.";

    [Fact]
    public void ParagraphDrawsOnAPageThatPassesQpdf()
    {
        var regular = TestFonts.NotoSans("Regular");
        var paragraph = PreparedParagraph.Prepare(new Paragraph([
            new TextSpan(German(Prose), new TextStyle(regular, 10)),
            new TextSpan(German(Underlined), new TextStyle(regular, 10, underline: true)),
            new TextSpan(German(Spaced), new TextStyle(regular, 10, letterSpacing: 0.5)),
        ]));
        var layout = paragraph.Fill(200);

        Assert.Empty(paragraph.Missing);
        Assert.True(layout.Lines.Count > 4, $"Only {layout.Lines.Count} lines.");
        Assert.All(layout.Lines, line => Assert.True(line.Width <= 200 + 1e-6, $"A line is {line.Width} wide."));

        var file = Write(layout, regular);
        Assert.Equal(file, Write(layout, regular));
        var path = GeneratedFiles.Save(file);
        Qpdf.Check(path);
    }

    private static byte[] Write(TextLayout layout, TrueTypeFont font) => PdfDocumentWriterTests.Write(
        document =>
        {
            var fonts = new Dictionary<TrueTypeFont, EmbeddedFont> { [font] = document.EmbedFont(font) };
            var page = document.BeginPage(595.2756, 841.8898);
            LayoutPainter.Paint(page, layout, fonts, 72, 72);
            document.EndPage(page);
        },
        CompressionLevel.Optimal);

    private static string German(string text) => text
        .Replace("{ae}", char.ConvertFromUtf32(0x00E4), StringComparison.Ordinal)
        .Replace("{oe}", char.ConvertFromUtf32(0x00F6), StringComparison.Ordinal)
        .Replace("{ue}", char.ConvertFromUtf32(0x00FC), StringComparison.Ordinal)
        .Replace("{ss}", char.ConvertFromUtf32(0x00DF), StringComparison.Ordinal)
        .Replace("{nbsp}", char.ConvertFromUtf32(0x00A0), StringComparison.Ordinal)
        .Replace("{euro}", char.ConvertFromUtf32(0x20AC), StringComparison.Ordinal)
        .Replace("{shy}", char.ConvertFromUtf32(0x00AD), StringComparison.Ordinal);
}
