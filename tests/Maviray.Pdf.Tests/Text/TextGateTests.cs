using System.IO.Compression;
using System.Text;
using Maviray.Pdf.Fonts;
using Maviray.Pdf.Tests.Fonts;
using Maviray.Pdf.Tests.Validation;
using Maviray.Pdf.Tests.Writer;
using Maviray.Pdf.Text;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Text;

/// <summary>
/// The P1 text gate (spec T-15): six paragraphs laid out by the text engine on one A4 page, checked without the filler
/// and by every validator. CI uploads the file in the <c>generated-pdfs</c> artifact as
/// <c>Maviray.Pdf.Tests/bin/Release/net10.0/generated/TextGateTests/TextGatePassesEveryCheck.pdf</c>, with the
/// expected lines beside it as <c>TextGatePassesEveryCheck.txt</c>. <c>docs/qa/checklists/viewer-check.md</c>
/// describes how to check it by hand. Keep this test's name, the layout and the checklist in step.
/// </summary>
public sealed class TextGateTests
{
    private const double PageWidth = 595.2756;
    private const double PageHeight = 841.8898;
    private const double Left = 72;
    private const double FirstTop = 36;
    private const double Gap = 8;

    private static readonly TrueTypeFont Regular = TestFonts.NotoSans("Regular");
    private static readonly TrueTypeFont Bold = TestFonts.NotoSans("Bold");
    private static readonly TrueTypeFont Italic = TestFonts.NotoSans("Italic");

    /// <summary>The prose at 160 points, the same 18 lines centred and right-aligned.</summary>
    private static readonly string[] NarrowProse =
    [
        "Vielen Dank f\u00FCr Ihren Auftrag. Die",
        "Rechnung f\u00FCr den Monat",
        "November enth\u00E4lt alle",
        "Leistungen, z.\u202FB. die Wartung der",
        "Server und die Kundenbetreuung.",
        "Bitte \u00FCberweisen Sie den",
        "Gesamtbetrag von 1.234,56\u00A0\u20AC bis",
        "zum 30. Dezember auf das",
        "angegebene Konto. F\u00FCr",
        "versp\u00E4tete Zahlungen berechnen",
        "wir eine Mahngeb\u00FChr von 20\u00A0\u20AC.",
        "Bei Fragen erreichen Sie unsere",
        "Buchhaltung jederzeit \u00FCber die E-",
        "Mail-Adresse auf der R\u00FCckseite",
        "oder telefonisch werktags unter",
        "der Kundennummer auf dem",
        "Deckblatt. Mit freundlichen",
        "Gr\u00FC\u00DFen aus K\u00F6ln",
    ];

    /// <summary>
    /// Each paragraph's lines as the engine copies them out, in the plan's order: U+202F and U+00A0 as themselves,
    /// and the hyphen at a soft hyphen as U+00AD.
    /// </summary>
    private static readonly string[][] ExpectedLines =
    [
        [
            "Vielen Dank f\u00FCr Ihren Auftrag. Die Rechnung",
            "f\u00FCr den Monat November enth\u00E4lt alle",
            "Leistungen, z.\u202FB. die Wartung der Server und",
            "die Kundenbetreuung. Bitte \u00FCberweisen Sie",
            "den Gesamtbetrag von 1.234,56\u00A0\u20AC bis zum 30.",
            "Dezember auf das angegebene Konto. F\u00FCr",
            "versp\u00E4tete Zahlungen berechnen wir eine",
            "Mahngeb\u00FChr von 20\u00A0\u20AC. Bei Fragen erreichen",
            "Sie unsere Buchhaltung jederzeit \u00FCber die E-",
            "Mail-Adresse auf der R\u00FCckseite oder",
            "telefonisch werktags unter der Kunden\u00AD",
            "nummer auf dem Deckblatt. Mit freundlichen",
            "Gr\u00FC\u00DFen aus K\u00F6ln",
        ],
        NarrowProse,
        NarrowProse,
        [
            "Zahlbar ohne Abzug innerhalb von 14 Tagen.",
            "Wichtig: Bitte geben Sie die Rechnungsnummer",
            "an. Der Gesamtbetrag von 1.234,56\u00A0\u20AC ist",
            "bis zum 30. Dezember f\u00E4llig, nicht bis zum 15.",
            "Der Vermerk BEZAHLT folgt.",
            "Faturan\u0131z \u0130stanbul \u015Fubemize \u00F6denebilir. \u0421\u0447\u0451\u0442",
            "\u043D\u0435\u043E\u0431\u0445\u043E\u0434\u0438\u043C\u043E \u043E\u043F\u043B\u0430\u0442\u0438\u0442\u044C \u0434\u043E 30 \u0434\u0435\u043A\u0430\u0431\u0440\u044F. \u03A4\u03BF",
            "\u03C4\u03B9\u03BC\u03BF\u03BB\u03CC\u03B3\u03B9\u03BF \u03B5\u03BE\u03BF\u03C6\u03BB\u03B5\u03AF\u03C4\u03B1\u03B9 \u03AD\u03C9\u03C2 \u03C4\u03B9\u03C2 30 \u0394\u03B5\u03BA\u03B5\u03BC\u03B2\u03C1\u03AF\u03BF\u03C5.",
        ],
        ["DE893704004", "40532013000"],
        ["K\u00F6ln, M\u00FCnchen"],
    ];

    [Fact]
    public void TextGatePassesEveryCheck()
    {
        // The decomposed input is decomposed, so a tool that composed this source couldn't make the NFC check vacuous.
        Assert.NotEqual(TextGateText.Decomposed, TextNormalizer.ToNfc(TextGateText.Decomposed));

        var paragraphs = Paragraphs();
        var layouts = paragraphs.Select(paragraph => PreparedParagraph.Prepare(paragraph.Paragraph).Fill(paragraph.Width)).ToArray();
        var tops = Tops(layouts);
        var expected = ExpectedLines.SelectMany(lines => lines).ToArray();

        // The plan's positions, which the viewer checklist describes, so a change to the line heights can't move the
        // page unseen.
        Assert.Equal([36, 200, 424, 648, 756.8, 788.8], tops, (a, b) => Math.Abs(a - b) < 1e-9);
        Assert.Equal(800.8, tops[^1] + layouts[^1].Height, 9);
        var file = Write(layouts, tops);
        var path = GeneratedFiles.Save(file);
        GeneratedFiles.SaveAs(Encoding.UTF8.GetBytes(string.Join('\n', expected) + "\n"), ".txt");

        // 1. Every character has a glyph in its span's font.
        var missing = layouts.SelectMany(layout => layout.Source.Missing).ToList();
        Assert.True(missing.Count == 0, $"Missing glyphs: {string.Join(", ", missing)}");

        // 2. Every line is a correct greedy line, by a check that doesn't use the filler.
        var problems = paragraphs
            .SelectMany((paragraph, i) => GreedyLineCheck.Problems(layouts[i], paragraph.Width).Select(problem => $"Paragraph {i + 1}: {problem}"))
            .ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));

        // 3. The lines' text, as the engine copies it out, is as written above.
        var lines = layouts.SelectMany(layout => layout.Lines.Select(LineText)).ToArray();
        Assert.Equal(expected, lines);

        // The mixed paragraph's underline ends its third line at the line's width, so not under the hanging space after
        // "ist", and goes on from the start of the fourth (T-11). The viewer checklist describes it so.
        var mixed = layouts[3].Lines;
        var ending = Assert.Single(mixed[2].Decorations, decoration => decoration.Kind == DecorationKind.Underline);
        var going = Assert.Single(mixed[3].Decorations, decoration => decoration.Kind == DecorationKind.Underline);
        Assert.Equal(mixed[2].Width, ending.X + ending.Width, 9);
        Assert.Equal(0, going.X, 9);

        // 4. The same page gives the same bytes, so the viewer check's SHA-256 identifies it.
        Assert.Equal(file, Write(layouts, tops));

        // 5. Ink inside every line's box, and nowhere else.
        var boxes = layouts.SelectMany((layout, i) => layout.Lines.Select(line => Box(line, tops[i]))).ToArray();
        var render = Assert.Single(Poppler.Render(path));
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
        var empty = Enumerable.Range(0, boxes.Length).Where(i => !inked[i]).Select(i => expected[i]).ToList();
        Assert.True(empty.Count == 0, $"No ink in the box of {string.Join(" | ", empty)}.");

        // 6. poppler reads the lines back, in order. The default mode joins the soft hyphen's line to the next, so the
        // physical-layout mode is read, which indents each line by its x position. Blind spots: whitespace at either
        // end of a line and empty lines are dropped, and pdftotext writes U+00A0 and U+202F as U+0020. Step 3 compares
        // them exactly, and the ink check finds each line at its offset.
        var extracted = Poppler.ExtractText(path, physicalLayout: true)
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();
        Assert.Equal(expected.Select(line => line.Replace('\u00A0', ' ').Replace('\u202F', ' ')), extracted);

        // 7. qpdf accepts the file.
        Qpdf.Check(path);

        // 8. pdffonts reports three embedded, subset, Unicode-mapped fonts, one per style.
        var fonts = Poppler.ListFonts(path);
        Assert.Equal(3, fonts.Count);
        Assert.All(fonts, font =>
        {
            Assert.Equal("CID TrueType", font.Type);
            Assert.True(font.Embedded && font.Subset && font.Unicode, $"{font.Name} is reported as {font}.");
            Assert.Matches(@"^[A-Z]{6}\+", font.Name);
        });
        Assert.Equal(["NotoSans-Bold", "NotoSans-Italic", "NotoSans-Regular"], fonts.Select(font => font.Name[7..]).Order());
    }

    // At the gate's widths each no-break sequence sits mid-line, where a breakable space of the same advance would give
    // the same lines. At 100, 130 and 190 points they reach a line's end, and must stay whole. The same prose with
    // breakable spaces of the same advance, U+0020 for U+00A0 and U+2009 for U+202F, does break inside one at these
    // widths, so the widths can't miss the sequences.
    [Fact]
    public void NoBreakSpacesKeepTheirNeighboursOnOneLine()
    {
        string[] sequences = ["z.\u202FB.", "1.234,56\u00A0\u20AC", "20\u00A0\u20AC"];
        double[] widths = [100, 130, 190];

        var inside = widths.SelectMany(width => LineEndsInside(TextGateText.Prose, sequences, width)).ToList();
        Assert.True(inside.Count == 0, string.Join("\n", inside));

        var control = widths.SelectMany(width => LineEndsInside(Breakable(TextGateText.Prose), [.. sequences.Select(Breakable)], width)).ToList();
        Assert.True(control.Count > 0, "With breakable spaces no line ends inside a sequence either, so these widths don't test the no-break spaces.");

        // UAX #14 allows no break on either side of a no-break space (LB12, LB12a).
        var breaks = LineBreaker.FindBreaks(TextGateText.Prose);
        for (var i = 0; i < TextGateText.Prose.Length; i++)
        {
            if (TextGateText.Prose[i] is '\u00A0' or '\u202F')
            {
                Assert.Equal(LineBreakKind.None, breaks[i]);
                Assert.Equal(LineBreakKind.None, breaks[i + 1]);
            }
        }
    }

    /// <summary>Each line end of the prose filled at <paramref name="width"/> that falls inside one of <paramref name="sequences"/>.</summary>
    private static IEnumerable<string> LineEndsInside(string prose, string[] sequences, double width)
    {
        var layout = PreparedParagraph.Prepare(new Paragraph([new TextSpan(prose, new SpanStyle(Regular, 10, TextGateText.LineHeight))])).Fill(width);
        var text = layout.Source.Text;
        foreach (var line in layout.Lines)
        {
            var end = line.Start + line.Length;
            foreach (var sequence in sequences)
            {
                for (var at = text.IndexOf(sequence, StringComparison.Ordinal); at >= 0; at = text.IndexOf(sequence, at + 1, StringComparison.Ordinal))
                {
                    if (at < end && end < at + sequence.Length)
                    {
                        yield return $"At {width}: a line ends at {end}, inside \"{sequence}\" at {at}.";
                    }
                }
            }
        }
    }

    private static string Breakable(string text) => text.Replace('\u00A0', ' ').Replace('\u202F', '\u2009');

    /// <summary>The six paragraphs of the plan, in order, each with its width.</summary>
    private static (Paragraph Paragraph, double Width)[] Paragraphs()
    {
        var style = new SpanStyle(Regular, 10, TextGateText.LineHeight);
        return
        [
            (new Paragraph([new TextSpan(TextGateText.Prose, style)]), 220),
            (new Paragraph([new TextSpan(TextGateText.Prose, style)], TextAlignment.Center), 160),
            (new Paragraph([new TextSpan(TextGateText.Prose, style)], TextAlignment.Right), 160),
            (new Paragraph(TextGateText.MixedSpans(Regular, Bold, Italic)), 220),
            (new Paragraph([new TextSpan(TextGateText.LongWord, style)]), 70),
            (new Paragraph([new TextSpan(TextGateText.Decomposed, style)]), 220),
        ];
    }

    /// <summary>Each paragraph's top, from the page's top: the first at 36 points, and each next 8 points below the one before.</summary>
    private static double[] Tops(TextLayout[] layouts)
    {
        var tops = new double[layouts.Length];
        tops[0] = FirstTop;
        for (var i = 1; i < layouts.Length; i++)
        {
            tops[i] = tops[i - 1] + layouts[i - 1].Height + Gap;
        }

        return tops;
    }

    /// <summary>A line's text as it copies out: its glyphs' code points, in order.</summary>
    private static string LineText(TextLine line)
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

    /// <summary>
    /// The line's box in PDF space: its offset and width from the left edge, and its top and height, with 3 points of
    /// margin across and 2 up and down for anti-aliasing and italic overhang.
    /// </summary>
    private static (double Left, double Right, double Bottom, double Top) Box(TextLine line, double top) => (
        Left + line.Offset - 3,
        Left + line.Offset + line.Width + 3,
        PageHeight - (top + line.Top + line.Height + 2),
        PageHeight - (top + line.Top - 2));

    private static byte[] Write(TextLayout[] layouts, double[] tops) => PdfDocumentWriterTests.Write(
        document =>
        {
            var fonts = new Dictionary<TrueTypeFont, EmbeddedFont>
            {
                [Regular] = document.EmbedFont(Regular),
                [Bold] = document.EmbedFont(Bold),
                [Italic] = document.EmbedFont(Italic),
            };
            var page = document.BeginPage(PageWidth, PageHeight);
            for (var i = 0; i < layouts.Length; i++)
            {
                LayoutPainter.Paint(page, layouts[i], fonts, Left, tops[i]);
            }

            document.EndPage(page);
        },
        CompressionLevel.Optimal);
}
