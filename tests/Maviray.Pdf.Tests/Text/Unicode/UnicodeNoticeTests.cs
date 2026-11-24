using System.Xml.Linq;

namespace Maviray.Pdf.Tests.Text.Unicode;

public sealed class UnicodeNoticeTests
{
    // The Unicode License requires its notice with every copy of data derived from the files (spec T-02).
    // Both texts have their line endings made LF before the comparison, because THIRD-PARTY-NOTICES.md follows each
    // developer's core.autocrlf setting. So this test can't see a line-ending difference; the licence file's own
    // checksum is pinned in UcdFilesTests.
    [Fact]
    public void NoticesCarryTheUnicodeLicence()
    {
        var notices = Lf(File.ReadAllText(RepositoryFiles.PathOf("THIRD-PARTY-NOTICES.md")));
        var licence = Lf(UcdFiles.ReadText("license.txt")).TrimEnd('\n');

        Assert.Contains("\n## Unicode Character Database\n", notices, StringComparison.Ordinal);
        Assert.Contains(licence, notices, StringComparison.Ordinal);
    }

    [Fact]
    public void PackagesShipTheNotices()
    {
        var props = XDocument.Load(RepositoryFiles.PathOf("src/Directory.Build.props"));

        var item = Assert.Single(
            props.Descendants("None"),
            e => ((string?)e.Attribute("Include"))?.EndsWith("THIRD-PARTY-NOTICES.md", StringComparison.Ordinal) == true);
        Assert.Equal("true", (string?)item.Attribute("Pack"));
        Assert.Equal("\\", (string?)item.Attribute("PackagePath"));
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);
}
