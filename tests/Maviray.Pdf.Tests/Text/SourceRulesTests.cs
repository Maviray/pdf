using System.Text.RegularExpressions;

namespace Maviray.Pdf.Tests.Text;

// The text engine must give the same result on every platform, so it uses neither ICU nor culture data (spec T-01).
// These tests read the source files, so they see calls and names, not what a call resolves to at run time.
public sealed class SourceRulesTests
{
    private static readonly Regex DotNetNormalization = new(@"\b(Normalize|IsNormalized)\s*\(", RegexOptions.CultureInvariant);
    private static readonly Regex CultureTypes = new(@"\b(CultureInfo|CompareInfo|TextInfo)\b", RegexOptions.CultureInvariant);

    [Fact]
    public void NothingInSrcCallsDotNetNormalization()
    {
        var matches = Matches("src", DotNetNormalization);

        Assert.True(matches.Count == 0, "Normalize or IsNormalized in src/:\n" + string.Join("\n", matches));
    }

    [Fact]
    public void TextEngineUsesNoCulture()
    {
        var matches = Matches("src/Maviray.Pdf/Text", CultureTypes);

        Assert.True(matches.Count == 0, "A culture type in src/Maviray.Pdf/Text/:\n" + string.Join("\n", matches));
    }

    private static List<string> Matches(string folder, Regex pattern)
    {
        var root = RepositoryFiles.PathOf(folder);
        var matches = new List<string>();
        foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var lines = File.ReadAllLines(path);
            for (var i = 0; i < lines.Length; i++)
            {
                if (pattern.IsMatch(lines[i]))
                {
                    matches.Add($"{Path.GetRelativePath(RepositoryFiles.Root, path)}:{i + 1}: {lines[i].Trim()}");
                }
            }
        }

        return matches;
    }
}
