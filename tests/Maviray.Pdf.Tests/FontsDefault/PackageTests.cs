using System.IO.Compression;
using System.Xml.Linq;
using Maviray.Pdf.Tests.Text;
using Maviray.Pdf.Tests.Validation;

namespace Maviray.Pdf.Tests.FontsDefault;

// The two packages (L-15): `dotnet pack` builds both from the build this test run uses, and each carries what the spec
// lists. The default fonts package depends on Maviray.Pdf at exactly its own version, and holds its fonts only inside
// its DLL.
public sealed class PackageTests
{
    private const string Version = "9.9.9-pack";

#if DEBUG
    private const string Configuration = "Debug";
#else
    private const string Configuration = "Release";
#endif

    [Fact]
    public void BothPackagesPack()
    {
        var output = Directory.CreateTempSubdirectory("maviray-pack-");
        try
        {
            Pack("src/Maviray.Pdf/Maviray.Pdf.csproj", output.FullName);
            Pack("src/Maviray.Pdf.Fonts.Default/Maviray.Pdf.Fonts.Default.csproj", output.FullName);

            using var core = ZipFile.OpenRead(Path.Combine(output.FullName, $"Maviray.Pdf.{Version}.nupkg"));
            using var fonts = ZipFile.OpenRead(Path.Combine(output.FullName, $"Maviray.Pdf.Fonts.Default.{Version}.nupkg"));
            var coreEntries = core.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.Ordinal);
            var fontEntries = fonts.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.Ordinal);

            Assert.Superset(Set("lib/net10.0/Maviray.Pdf.dll", "README.md", "LICENSE", "THIRD-PARTY-NOTICES.md"), coreEntries);
            Assert.Superset(Set("lib/net10.0/Maviray.Pdf.Fonts.Default.dll", "README.md", "LICENSE", "THIRD-PARTY-NOTICES.md", "OFL.txt"), fontEntries);
            Assert.DoesNotContain(fontEntries, entry => entry.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase));
            Assert.Equal([("Maviray.Pdf", $"[{Version}]")], Dependencies(fonts.GetEntry("Maviray.Pdf.Fonts.Default.nuspec")!));
        }
        finally
        {
            output.Delete(recursive: true);
        }
    }

    // Packs a project from its existing build. No build server or reused node outlives the command, because one would
    // hold the output pipes open after it exits.
    private static void Pack(string project, string output)
    {
        var result = ExternalTool.RunFile(
            ExternalTool.Locate("dotnet", "Install the .NET SDK to run this check."),
            "pack", RepositoryFiles.PathOf(project), "--no-build", "--nologo", "--disable-build-servers", "-nodeReuse:false", "-maxCpuCount:1",
            "-c", Configuration, "-o", output, $"-p:Version={Version}");

        Assert.True(result.ExitCode == 0, $"dotnet pack {project} failed:\n{result.StandardOutput}\n{result.StandardError}");
    }

    private static HashSet<string> Set(params string[] entries) => new(entries, StringComparer.Ordinal);

    private static List<(string Id, string Version)> Dependencies(ZipArchiveEntry nuspec)
    {
        using var stream = nuspec.Open();
        return [.. XDocument.Load(stream).Descendants()
            .Where(element => element.Name.LocalName == "dependency")
            .Select(element => ((string)element.Attribute("id")!, (string)element.Attribute("version")!))];
    }
}
