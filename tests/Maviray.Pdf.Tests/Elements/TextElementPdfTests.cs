using System.IO.Compression;
using System.Text;
using Maviray.Pdf.Drawing;
using Maviray.Pdf.Elements;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Tests.Drawing;
using Maviray.Pdf.Tests.Fonts;
using Maviray.Pdf.Tests.Validation;
using Maviray.Pdf.Tests.Writer;
using Maviray.Pdf.Text;

namespace Maviray.Pdf.Tests.Elements;

// P1.11 is done when a multi-page paragraph splits by line and every page passes qpdf (spec, Work items).
public sealed class TextElementPdfTests
{
    private const string Sentence =
        "Die Rechnung für den Monat November enthält alle Leistungen, die wir für Sie erbracht haben, " +
        "und wir bitten Sie, den Betrag bis zum Ende des Monats auf das angegebene Konto zu überweisen. ";

    private static readonly FontRegistry Fonts = new FontRegistry()
        .Register("Noto Sans", File.ReadAllBytes(TestFonts.NotoSansPath("Regular")));

    private static readonly TextStyle Style = new TextStyle().FontSize(10).LineHeight(1.3);

    [Fact]
    public void AMultiPageParagraphSplitsByLineAndPassesQpdf()
    {
        var recorded = new RecordingCanvas();
        var count = LayoutEngine.Generate(Sets(), recorded);
        var file = PdfDocumentWriterTests.Write(document => LayoutEngine.Generate(Sets(), new PdfCanvas(document)), CompressionLevel.Optimal);
        var path = GeneratedFiles.Save(file);

        // The layout's own lines, page by page: the content's, then the footer's.
        var expected = recorded.Pages.Select(page => page.Calls.Where(call => call.Kind == "TextLine").Select(LineText).ToList()).ToList();
        var content = expected.SelectMany(lines => lines.Take(lines.Count - 1)).ToList();
        Assert.True(count >= 4, $"Only {count} pages.");
        Assert.Equal(Enumerable.Range(1, count).Select(n => $"Seite {n} von {count}"), expected.Select(lines => lines[^1]));
        Assert.Equal(AllLines(), content);

        Qpdf.Check(path);
        var pages = Poppler.ExtractText(path, physicalLayout: true).Split('\f');
        Assert.Equal(count, pages.Length);
        for (var i = 0; i < count; i++)
        {
            var lines = pages[i].Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
            Assert.Equal(expected[i], lines);
        }
    }

    /// <summary>One A5 page set: the paragraph, and a footer of "Seite n von N".</summary>
    private static PageSet[] Sets()
    {
        var paragraph = new TextElement([new TextRun(TextRunKind.Text, Prose(), new TextStyle())], Style, new TextStyle(), TextAlignment.Left, Fonts);
        var footer = new TextElement(
            [
                new TextRun(TextRunKind.Text, "Seite ", new TextStyle()),
                new TextRun(TextRunKind.PageNumber, "", new TextStyle()),
                new TextRun(TextRunKind.Text, " von ", new TextStyle()),
                new TextRun(TextRunKind.PageCount, "", new TextStyle()),
            ],
            Style,
            new TextStyle(),
            TextAlignment.Center,
            Fonts);
        return [new PageSet(419.53, 595.28, new Margins(40, 40, 40, 40), null, null, paragraph, footer)];
    }

    private static string Prose() => string.Concat(Enumerable.Repeat(Sentence, 50)).TrimEnd();

    /// <summary>Every line of the paragraph at the content's width, filled once on its own.</summary>
    private static List<string> AllLines()
    {
        var spans = new[] { new Maviray.Pdf.Text.TextSpan(Prose(), new SpanStyle(Fonts.Match("Noto Sans", 400, false)!.Face.Font, 10, 1.3)) };
        var layout = PreparedParagraph.Prepare(new Paragraph(spans)).Fill(419.53 - 80);
        return [.. layout.Lines.Select(line => Text(line))];
    }

    private static string LineText(CanvasCall call) => Text(call.Layout!.Lines[call.LineIndex]);

    private static string Text(TextLine line)
    {
        var text = new StringBuilder();
        foreach (var run in line.Runs)
        {
            foreach (var glyph in run.Glyphs)
            {
                text.Append(char.ConvertFromUtf32(glyph.CodePoint));
            }
        }

        return text.ToString();
    }
}
