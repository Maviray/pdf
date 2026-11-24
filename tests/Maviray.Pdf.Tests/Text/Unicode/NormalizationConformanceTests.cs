using System.Globalization;
using Maviray.Pdf.Text;

namespace Maviray.Pdf.Tests.Text.Unicode;

// Unicode's own conformance test for NFC (spec T-04): every line of NormalizationTest.txt 16.0.0, in all six parts.
public sealed class NormalizationConformanceTests
{
    private const int TestLines = 19_965;
    private const int Part1CodePoints = 17_085;

    [Fact]
    public void EveryNormalizationTestLinePasses()
    {
        var lines = 0;
        var failures = new List<string>();
        foreach (var (lineNumber, fields) in UcdFiles.ReadRecords("NormalizationTest.txt"))
        {
            if (fields[0].StartsWith('@'))
            {
                continue;
            }

            lines++;
            var c = fields.Take(5).Select(Text).ToArray();

            // c2 == toNFC(c1) == toNFC(c2) == toNFC(c3), and c4 == toNFC(c4) == toNFC(c5).
            Check(lineNumber, "c1", c[0], c[1], failures);
            Check(lineNumber, "c2", c[1], c[1], failures);
            Check(lineNumber, "c3", c[2], c[1], failures);
            Check(lineNumber, "c4", c[3], c[3], failures);
            Check(lineNumber, "c5", c[4], c[3], failures);
        }

        Assert.Equal(TestLines, lines);
        Assert.True(failures.Count == 0, $"{failures.Count} failures, the first 20:\n{string.Join("\n", failures.Take(20))}");
    }

    [Fact]
    public void EveryOtherCodePointIsUnchanged()
    {
        var part1 = new HashSet<int>();
        var inPart1 = false;
        foreach (var (_, fields) in UcdFiles.ReadRecords("NormalizationTest.txt"))
        {
            if (fields[0].StartsWith('@'))
            {
                inPart1 = fields[0] == "@Part1";
            }
            else if (inPart1)
            {
                part1.Add(Hex(fields[0]));
            }
        }

        var changed = new List<string>();
        for (var codePoint = 0; codePoint <= 0x10FFFF; codePoint++)
        {
            if (codePoint is >= 0xD800 and <= 0xDFFF || part1.Contains(codePoint))
            {
                continue;
            }

            var text = char.ConvertFromUtf32(codePoint);
            if (TextNormalizer.ToNfc(text) != text)
            {
                changed.Add($"U+{codePoint:X4}");
            }
        }

        Assert.Equal(Part1CodePoints, part1.Count);
        Assert.Empty(changed.Take(20));
    }

    private static void Check(int lineNumber, string field, string input, string expected, List<string> failures)
    {
        var actual = TextNormalizer.ToNfc(input);
        if (actual != expected)
        {
            failures.Add($"line {lineNumber}, NFC({field}): expected {CodePoints(expected)}, got {CodePoints(actual)}");
        }
    }

    private static string Text(string codePoints) =>
        string.Concat(codePoints.Split(' ').Select(h => char.ConvertFromUtf32(Hex(h))));

    private static int Hex(string text) => int.Parse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);

    private static string CodePoints(string text) =>
        string.Join(" ", text.EnumerateRunes().Select(r => r.Value.ToString("X4", CultureInfo.InvariantCulture)));
}
