using System.IO.Compression;
using System.Text;
using Maviray.Pdf.Tests.Fonts;
using Maviray.Pdf.Tests.Validation;

namespace Maviray.Pdf.Tests.Writer;

/// <summary>
/// The P1 fonts gate (spec F-15): six languages in the four Noto Sans styles on one A4 page, checked by every
/// validator. CI uploads the file in the <c>generated-pdfs</c> artifact as
/// <c>Maviray.Pdf.Tests/bin/Release/net10.0/generated/FontsGateTests/FontsGatePassesEveryValidator.pdf</c>, with the
/// expected text beside it as <c>FontsGatePassesEveryValidator.txt</c>. <c>docs/qa/checklists/viewer-check.md</c>
/// describes how to check it by hand. Keep this test's name, the layout and the checklist in step.
/// </summary>
public sealed class FontsGateTests
{
    private const double Size = 14;
    private const double Left = 72;

    private static readonly string[] Styles = ["Regular", "Bold", "Italic", "BoldItalic"];

    // F-15's lines, in its order: German, Turkish, Azerbaijani, Russian, Ukrainian, Greek.
    private static readonly string[] Languages =
    [
        "Grüße aus Köln: Äpfel, Öl und Übermaß",
        "Iğdır'da ılık bir gün; İstanbul, Şişli",
        "Əli Gəncədə yaşayır",
        "Счёт № 42 от 20 ноября",
        "Ґанок, їжак і євро",
        "Τιμολόγιο για την Αθήνα",
    ];

    /// <summary>Line <c>i</c> is language <c>i mod 6</c> in style <c>i ÷ 6</c>; each style block starts 12 points lower.</summary>
    private static readonly (string Style, string Text, double Baseline)[] Lines =
        Enumerable.Range(0, 24).Select(i => (Styles[i / 6], Languages[i % 6], 790.0 - (24 * i) - (12 * (i / 6)))).ToArray();

    [Fact]
    public void FontsGatePassesEveryValidator()
    {
        var file = Write();
        var path = GeneratedFiles.Save(file);
        GeneratedFiles.SaveAs(Encoding.UTF8.GetBytes(string.Join('\n', Lines.Select(line => line.Text)) + "\n"), ".txt");

        // 1. No character falls back to .notdef.
        foreach (var style in Styles)
        {
            var font = TestFonts.NotoSans(style);
            var missing = Languages.SelectMany(line => line.EnumerateRunes()).Where(rune => font.GetGlyphId(rune.Value) == 0).ToList();
            Assert.True(missing.Count == 0, $"Noto Sans {style} has no glyph for {string.Join(", ", missing)}.");
        }

        // 2. The same page gives the same bytes, so the viewer check's SHA-256 identifies it.
        Assert.Equal(file, Write());

        // 3. Ink inside every line's box, and nowhere else.
        var render = Assert.Single(Poppler.Render(path));
        var boxes = Lines.Select(Box).ToArray();
        Assert.True(boxes.Sum(b => (b.Right - b.Left) * (b.Top - b.Bottom)) < render.Width * render.Height / 2.0, "The line boxes cover half the page or more.");
        var inked = new bool[boxes.Length];
        var stray = new List<string>();
        for (var row = 0; row < render.Height; row++)
        {
            var y = render.Height - row - 0.5; // the pixel's centre, in PDF space: 72 dpi puts one point in one pixel
            for (var x = 0; x < render.Width; x++)
            {
                var (r, g, b) = render.GetPixel(x, row);
                var box = Array.FindIndex(boxes, box => x + 0.5 >= box.Left && x + 0.5 <= box.Right && y >= box.Bottom && y <= box.Top);
                if (box >= 0)
                {
                    inked[box] |= r < 128 || g < 128 || b < 128;
                }
                else if ((r, g, b) != (255, 255, 255))
                {
                    stray.Add($"({x}, {y:0.#})");
                }
            }
        }

        Assert.True(stray.Count == 0, $"{stray.Count} pixels outside the lines aren't white. The first: {string.Join(" ", stray.Take(10))}");
        var empty = Enumerable.Range(0, Lines.Length).Where(i => !inked[i]).Select(i => $"{Lines[i].Style}: {Lines[i].Text}").ToList();
        Assert.True(empty.Count == 0, $"No ink in the box of {string.Join(" | ", empty)}.");

        // 4. The text copies out exactly, read by poppler. xpdf 4.00's default mode joins each block's lines into one
        // line, so the check comes after the render, which needs poppler. Blind spot: empty lines and line-end whitespace are dropped, as in the P0 gate.
        var extracted = Poppler.ExtractText(path)
            .Split('\n')
            .Select(line => line.TrimEnd())
            .Where(line => line.Length > 0)
            .ToArray();
        Assert.Equal(Lines.Select(line => line.Text), extracted);

        // 5. qpdf accepts the file.
        Qpdf.Check(path);

        // 6. pdffonts reports four embedded, subset, Unicode-mapped fonts, one per style.
        var fonts = Poppler.ListFonts(path);
        Assert.Equal(4, fonts.Count);
        Assert.All(fonts, font =>
        {
            Assert.Equal("CID TrueType", font.Type);
            Assert.True(font.Embedded && font.Subset && font.Unicode, $"{font.Name} is reported as {font}.");
            Assert.Matches(@"^[A-Z]{6}\+", font.Name);
        });
        Assert.Equal(Styles.Select(style => $"NotoSans-{style}").Order(), fonts.Select(font => font.Name[7..]).Order());
    }

    /// <summary>
    /// The line's box in PDF space: its advance widths from the left edge, and hhea's ascender and descender from the
    /// baseline, with 3 points of margin across and 2 up and down for anti-aliasing and italic overhang.
    /// </summary>
    private static (double Left, double Right, double Bottom, double Top) Box((string Style, string Text, double Baseline) line)
    {
        var font = TestFonts.NotoSans(line.Style);
        var scale = Size / font.UnitsPerEm;
        var width = line.Text.EnumerateRunes().Sum(rune => font.GetAdvanceWidth(font.GetGlyphId(rune.Value))) * scale;
        return (
            Left - 3,
            Left + width + 3,
            line.Baseline + (font.HorizontalHeaderMetrics.Descender * scale) - 2,
            line.Baseline + (font.HorizontalHeaderMetrics.Ascender * scale) + 2);
    }

    private static byte[] Write() => PdfDocumentWriterTests.Write(
        document =>
        {
            var fonts = Styles.ToDictionary(style => style, style => document.EmbedFont(TestFonts.NotoSans(style)));
            var page = document.BeginPage(595.2756, 841.8898);
            page.Content.BeginText();
            foreach (var (style, text, baseline) in Lines)
            {
                var font = fonts[style];
                page.Content.SetFont(font, Size).SetTextMatrix(1, 0, 0, 1, Left, baseline).ShowGlyphs(EmbeddedText.Encode(font, text));
            }

            page.Content.EndText();
            document.EndPage(page);
        },
        CompressionLevel.Optimal);
}
