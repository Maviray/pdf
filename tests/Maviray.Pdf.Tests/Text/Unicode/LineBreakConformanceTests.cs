using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Maviray.Pdf.Text;

namespace Maviray.Pdf.Tests.Text.Unicode;

// Unicode's own conformance test for line breaking (spec T-06): every line of LineBreakTest.txt 16.0.0.
public sealed class LineBreakConformanceTests
{
    private const int TestLines = 16_672;
    private const char NoBreak = (char)0xD7; // the multiplication sign the file writes for "no break"
    private const char Break = (char)0xF7; // the division sign the file writes for "break"

    private static readonly Regex RuleNumbers = new(@"\[(\d+\.\d+)\]", RegexOptions.CultureInvariant);

    [Fact]
    public void EveryLineBreakTestPasses()
    {
        var lines = 0;
        var failures = new List<string>();
        foreach (var (lineNumber, text, positions, breaks, rules) in ReadTests())
        {
            lines++;
            var kinds = LineBreaker.FindBreaks(text);
            for (var b = 1; b < positions.Count; b++)
            {
                var actual = kinds[positions[b]];
                if ((actual != LineBreakKind.None) != breaks[b])
                {
                    var rule = b < rules.Count ? rules[b] : "?";
                    failures.Add(
                        $"line {lineNumber}, boundary {b} of {CodePoints(text)}: expected {(breaks[b] ? "break" : "no break")}, " +
                        $"got {actual} (rule {rule})");
                }
            }
        }

        Assert.Equal(TestLines, lines);
        Assert.True(failures.Count == 0, $"{failures.Count} failures, the first 20:\n{string.Join("\n", failures.Take(20))}");
    }

    /// <summary>
    /// Each test line: its text, the UTF-16 position of each code point boundary from the start to the end, whether
    /// the file marks a break there, and the rule its comment names for each boundary.
    /// </summary>
    internal static IEnumerable<(int LineNumber, string Text, List<int> Positions, List<bool> Breaks, List<string> Rules)> ReadTests()
    {
        var lines = UcdFiles.ReadText("LineBreakTest.txt").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var hash = line.IndexOf('#', StringComparison.Ordinal);
            var data = (hash < 0 ? line : line[..hash]).Trim();
            if (data.Length == 0)
            {
                continue;
            }

            var text = new StringBuilder();
            var positions = new List<int>();
            var breaks = new List<bool>();
            foreach (var token in data.Split((char[])[' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.Length == 1 && token[0] is NoBreak or Break)
                {
                    positions.Add(text.Length);
                    breaks.Add(token[0] == Break);
                }
                else
                {
                    text.Append(char.ConvertFromUtf32(int.Parse(token, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)));
                }
            }

            var rules = hash < 0 ? [] : RuleNumbers.Matches(line[hash..]).Select(m => m.Groups[1].Value).ToList();
            yield return (i + 1, text.ToString(), positions, breaks, rules);
        }
    }

    private static string CodePoints(string text) =>
        string.Join(" ", text.EnumerateRunes().Select(r => r.Value.ToString("X4", CultureInfo.InvariantCulture)));
}
