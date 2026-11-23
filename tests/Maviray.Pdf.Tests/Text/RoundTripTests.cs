using System.Globalization;
using System.Text;
using Maviray.Pdf.Text;

namespace Maviray.Pdf.Tests.Text;

// The measured width round-trips (spec T-09): filling a paragraph at the widest line width of an earlier fill gives the
// same lines, whenever no line of the earlier fill was wider than its width. Random paragraphs from a logged seed.
public sealed class RoundTripTests(ITestOutputHelper output)
{
    private const int Paragraphs = 2000;

    // Letters, W, spaces, a tab, U+00A0, U+00AD, '-', U+2010, U+0301 and a line feed, by code point.
    private static readonly int[] Alphabet =
    [
        'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'k', 'l', 'm', 'n', 'o', 'p', 'r', 's', 't', 'u', 'W',
        ' ', ' ', ' ', ' ', 0x0009, 0x00A0, 0x00AD, '-', 0x2010, 0x0301, '\n',
    ];

    [Fact]
    public void FillingAtTheMeasuredWidthGivesTheSameLines()
    {
        var seed = int.TryParse(Environment.GetEnvironmentVariable("SEED"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var given)
            ? given
            : Random.Shared.Next();
        output.WriteLine($"re-run with SEED={seed}");
        var random = new Random(seed);
        var failures = new List<string>();

        for (var n = 0; n < Paragraphs && failures.Count < 5; n++)
        {
            var paragraph = RandomParagraph(random);
            var prepared = PreparedParagraph.Prepare(paragraph);
            foreach (var width in new[] { 1 + (random.NextDouble() * 299), double.PositiveInfinity })
            {
                var first = prepared.Fill(width);
                if (first.Width == 0 || first.Lines.Any(line => line.Width > width + 1e-6))
                {
                    continue;
                }

                var again = prepared.Fill(first.Width);
                if (!Ranges(first).SequenceEqual(Ranges(again)))
                {
                    failures.Add($"paragraph {n} at {width.ToString("R", CultureInfo.InvariantCulture)}: {Describe(paragraph)}");
                }
            }
        }

        Assert.True(failures.Count == 0, $"SEED={seed}: {failures.Count} failures:\n{string.Join("\n", failures)}");
    }

    private static Paragraph RandomParagraph(Random random)
    {
        var spans = new List<TextSpan>();
        var count = 1 + random.Next(4);
        for (var s = 0; s < count; s++)
        {
            var text = new StringBuilder();
            var length = random.Next(41);
            for (var i = 0; i < length; i++)
            {
                text.Append(char.ConvertFromUtf32(Alphabet[random.Next(Alphabet.Length)]));
            }

            // The awkward values by construction: every paragraph has a soft hyphen and a run of spaces.
            if (s == 0)
            {
                text.Insert(random.Next(text.Length + 1), "aa" + (char)0x00AD + "bb");
                text.Insert(random.Next(text.Length + 1), "   ");
            }

            var style = LayoutTestFont.Style(
                size: 6 + (random.NextDouble() * 14),
                lineHeight: random.Next(3) == 0 ? 0.8 + random.NextDouble() : null,
                letterSpacing: -0.5 + (random.NextDouble() * 2.5),
                underline: random.Next(3) == 0,
                strikethrough: random.Next(4) == 0);
            spans.Add(new TextSpan(text.ToString(), style));
        }

        return new Paragraph(spans, (TextAlignment)random.Next(3));
    }

    private static IEnumerable<(int Start, int Length)> Ranges(TextLayout layout) => layout.Lines.Select(l => (l.Start, l.Length));

    private static string Describe(Paragraph paragraph) => string.Join(
        " | ",
        paragraph.Spans.Select(s =>
            $"size {s.Style.Size:F3}, spacing {s.Style.LetterSpacing:F3}: " +
            string.Join(" ", s.Text.EnumerateRunes().Select(r => r.Value.ToString("X4", CultureInfo.InvariantCulture)))));
}
