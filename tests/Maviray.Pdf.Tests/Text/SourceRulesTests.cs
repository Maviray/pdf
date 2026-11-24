using System.Text.RegularExpressions;

namespace Maviray.Pdf.Tests.Text;

// Rules the compiler can't enforce, checked on the source files. They see calls and names, not what a call resolves to
// at run time. Each test also checks that it scanned the file it exists to guard, so it can't pass by finding nothing.
public sealed class SourceRulesTests
{
    // The text engine must give the same result on every platform, so it uses neither ICU nor culture data (spec T-01).
    // Every .NET normalization API depends on ICU, not only Normalize and IsNormalized.
    private static readonly Regex DotNetNormalization = new(
        @"\b(Normalize|IsNormalized|TryNormalize|GetNormalizedLength)\s*\(|\bNormalizationForm\b",
        RegexOptions.CultureInvariant);

    private static readonly Regex CultureTypes = new(@"\b(CultureInfo|CompareInfo|TextInfo)\b", RegexOptions.CultureInvariant);

    private static readonly Regex PartialType = new(@"\bpartial\s+(class|struct|record|interface)\b", RegexOptions.CultureInvariant);

    // CONTRIBUTING.md: one type per file, and no partial classes, in the tests as in the library.
    [Fact]
    public void NoSourceDeclaresAPartialType()
    {
        var (scannedSrc, matchesSrc) = Scan("src", PartialType.IsMatch);
        var (scannedTests, matchesTests) = Scan("tests", PartialType.IsMatch);

        Assert.Contains("src/Maviray.Pdf/Text/TextNormalizer.cs", scannedSrc);
        Assert.Contains("tests/Maviray.Pdf.Tests/Validation/Poppler.cs", scannedTests);
        var matches = matchesSrc.Concat(matchesTests).ToList();
        Assert.True(matches.Count == 0, "A partial type:\n" + string.Join("\n", matches));
    }

    [Fact]
    public void NothingInSrcCallsDotNetNormalization()
    {
        var (scanned, matches) = Scan("src", DotNetNormalization.IsMatch);

        Assert.Contains("src/Maviray.Pdf/Text/TextNormalizer.cs", scanned);
        Assert.True(matches.Count == 0, ".NET normalization in src/:\n" + string.Join("\n", matches));
    }

    [Fact]
    public void TextEngineUsesNoCulture()
    {
        var (scanned, matches) = Scan("src/Maviray.Pdf/Text", CultureTypes.IsMatch);

        Assert.Contains("src/Maviray.Pdf/Text/TextNormalizer.cs", scanned);
        Assert.True(matches.Count == 0, "A culture type in src/Maviray.Pdf/Text/:\n" + string.Join("\n", matches));
    }

    // A decomposed input written as raw characters looks like its expected result, and a tool that normalizes the
    // file on save would turn the test into an identity check that still passes. So the text tests spell every
    // character beyond ASCII as an escape.
    [Fact]
    public void TextTestsSpellNonAsciiAsEscapes()
    {
        var (scanned, matches) = Scan("tests/Maviray.Pdf.Tests/Text", line => line.Any(c => c > 127));

        Assert.Contains("tests/Maviray.Pdf.Tests/Text/TextNormalizerTests.cs", scanned);
        Assert.True(matches.Count == 0, "Raw characters beyond ASCII in the text tests:\n" + string.Join("\n", matches));
    }

    /// <summary>
    /// The repository-relative paths of the <c>.cs</c> files under a folder, outside any <c>bin</c> or <c>obj</c>
    /// folder within it, and each matching line as <c>path:line: text</c>.
    /// </summary>
    private static (List<string> Scanned, List<string> Matches) Scan(string folder, Func<string, bool> isMatch)
    {
        var scanned = new List<string>();
        var matches = new List<string>();
        foreach (var path in Directory.EnumerateFiles(RepositoryFiles.PathOf(folder), "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(RepositoryFiles.Root, path).Replace(Path.DirectorySeparatorChar, '/');
            if (relative.Split('/').Any(segment => segment is "bin" or "obj"))
            {
                continue;
            }

            scanned.Add(relative);
            var lines = File.ReadAllLines(path);
            for (var i = 0; i < lines.Length; i++)
            {
                if (isMatch(lines[i]))
                {
                    matches.Add($"{relative}:{i + 1}: {lines[i].Trim()}");
                }
            }
        }

        scanned.Sort(StringComparer.Ordinal);
        matches.Sort(StringComparer.Ordinal);
        return (scanned, matches);
    }
}
