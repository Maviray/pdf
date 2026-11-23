using System.Text;
using System.Text.RegularExpressions;
using Maviray.Pdf.Fonts;
using Maviray.Pdf.Tests.Fonts;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

/// <summary>
/// Embedded fonts in a whole document (spec F-10): one embedded font per TrueType font, one sequence of resource names
/// shared with the standard font, each page listing only the fonts it set, and each used font written once at close.
/// </summary>
public sealed class EmbeddedFontDocumentTests
{
    private const string Line = "Grüße aus Köln: Äpfel, Öl und Übermaß";

    [Fact]
    public void StandardAndEmbeddedFontsShareOneSequenceOfNames()
    {
        var text = Text(PdfDocumentWriterTests.Write(document =>
        {
            var noto = document.EmbedFont(TestFonts.NotoSans("Regular"));
            var page = document.BeginPage(595.2756, 841.8898);
            page.Content.BeginText()
                .SetFont(StandardFont.Helvetica, 12).ShowText("a")
                .SetFont(noto, 12).ShowGlyphs(EmbeddedText.Encode(noto, "a"))
                .EndText();
            document.EndPage(page);
        }));

        Assert.Matches(@"/Font <</F1 \d+ 0 R /F2 \d+ 0 R>>", text);
    }

    [Fact]
    public void EachPageListsOnlyTheFontsItSet()
    {
        var text = Text(PdfDocumentWriterTests.Write(document =>
        {
            var noto = document.EmbedFont(TestFonts.NotoSans("Regular"));
            var first = document.BeginPage(595.2756, 841.8898);
            first.Content.BeginText().SetFont(noto, 12).ShowGlyphs(EmbeddedText.Encode(noto, "a")).EndText();
            document.EndPage(first);
            var second = document.BeginPage(595.2756, 841.8898);
            second.Content.BeginText().SetFont(StandardFont.Helvetica, 12).ShowText("b").EndText();
            document.EndPage(second);
        }));

        Assert.Equal(["/Font <</F1", "/Font <</F2"], Regex.Matches(text, @"/Font <</F\d").Select(m => m.Value));
    }

    [Fact]
    public void AFontOnTwoPagesIsWrittenOnce()
    {
        var text = Text(PdfDocumentWriterTests.Write(document =>
        {
            var noto = document.EmbedFont(TestFonts.NotoSans("Regular"));
            for (var i = 0; i < 2; i++)
            {
                var page = document.BeginPage(595.2756, 841.8898);
                page.Content.BeginText().SetFont(noto, 12).ShowGlyphs(EmbeddedText.Encode(noto, Line)).EndText();
                document.EndPage(page);
            }
        }));

        Assert.Single(Regex.Matches(text, "/Subtype /Type0 "));
        Assert.Single(Regex.Matches(text, "/Subtype /CIDFontType2 "));
    }

    [Fact]
    public void AFontNoPageSetIsNotWritten()
    {
        var text = Text(PdfDocumentWriterTests.Write(document =>
        {
            document.EmbedFont(TestFonts.NotoSans("Regular"));
            var page = document.BeginPage(595.2756, 841.8898);
            page.Content.BeginText().SetFont(StandardFont.Helvetica, 12).ShowText("a").EndText();
            document.EndPage(page);
        }));

        Assert.DoesNotContain("/Type0", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WritingTwiceGivesTheSameBytes()
    {
        Assert.Equal(OneLine(TestFonts.NotoSans("Regular")), OneLine(TestFonts.NotoSans("Regular")));
    }

    [Fact]
    public void EmbedFontAfterCloseThrows()
    {
        using var document = new PdfDocumentWriter(new MemoryStream());
        var page = document.BeginPage(100, 100);
        document.EndPage(page);
        document.Close();

        Assert.Throws<InvalidOperationException>(() => document.EmbedFont(TestFonts.NotoSans("Regular")));
    }

    [Fact]
    public void ClosingPastTheGlyphLimitThrows()
    {
        var builder = new TestFontBuilder().CompositeGlyph(700, new TestComponent(2, 0x0002), new TestComponent(3, 0x0002));
        var font = TrueTypeFont.Load(builder.Build());

        Assert.Throws<InvalidOperationException>(() => PdfDocumentWriterTests.Write(document =>
        {
            var embedded = document.EmbedFont(font);
            for (var i = 0; i < 65_534; i++)
            {
                embedded.Encode(4, 0x10000 + i);
            }

            var page = document.BeginPage(100, 100);
            page.Content.BeginText().SetFont(embedded, 12).ShowGlyphs([1]).EndText();
            document.EndPage(page);
        }));
    }

    private static byte[] OneLine(TrueTypeFont font) => PdfDocumentWriterTests.Write(document =>
    {
        var embedded = document.EmbedFont(font);
        var page = document.BeginPage(595.2756, 841.8898);
        page.Content.BeginText().SetFont(embedded, 24).SetTextMatrix(1, 0, 0, 1, 72, 750)
            .ShowGlyphs(EmbeddedText.Encode(embedded, Line)).EndText();
        document.EndPage(page);
    });

    private static string Text(byte[] file) => Encoding.Latin1.GetString(file);
}
