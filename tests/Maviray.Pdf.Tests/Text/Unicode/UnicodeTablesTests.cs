using System.Text;

namespace Maviray.Pdf.Tests.Text.Unicode;

// The tables in src/Maviray.Pdf/Text/Unicode/ are generated from the UCD files and never edited by hand (spec T-02).
// With MAVIRAY_PDF_WRITE_UNICODE_TABLES=true, the first test writes them instead of comparing.
public sealed class UnicodeTablesTests(ITestOutputHelper output)
{
    private const string WriteVariable = "MAVIRAY_PDF_WRITE_UNICODE_TABLES";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    [Fact]
    public void CommittedTablesAreCurrent()
    {
        var write = string.Equals(Environment.GetEnvironmentVariable(WriteVariable), "true", StringComparison.OrdinalIgnoreCase);

        foreach (var file in UnicodeTableGenerator.Generate())
        {
            var path = Path.Combine(UnicodeTableGenerator.TablesFolder, file.FileName);
            var expected = Utf8NoBom.GetBytes(file.Content);

            if (write)
            {
                Directory.CreateDirectory(UnicodeTableGenerator.TablesFolder);
                File.WriteAllBytes(path, expected);
                output.WriteLine($"Wrote {path} ({expected.Length:N0} bytes).");
                continue;
            }

            Assert.True(File.Exists(path), $"{path} is missing. Run the tests with {WriteVariable}=true to write it.");
            var actual = File.ReadAllBytes(path);
            var difference = FirstDifference(expected, actual);
            Assert.True(
                difference < 0,
                $"{path} is stale: it first differs from the generated content at byte {difference}. " +
                $"Run the tests with {WriteVariable}=true to rewrite it.");
        }
    }

    [Fact]
    public void TablesFolderHoldsOnlyGeneratedFiles()
    {
        var generated = UnicodeTableGenerator.Generate().Select(f => f.FileName).Order(StringComparer.Ordinal);
        var committed = Directory.GetFiles(UnicodeTableGenerator.TablesFolder, "*.cs")
            .Select(p => Path.GetFileName(p))
            .Order(StringComparer.Ordinal);

        Assert.Equal(generated, committed);
    }

    [Fact]
    public void GenerationIsDeterministic()
    {
        var first = UnicodeTableGenerator.Generate();
        var second = UnicodeTableGenerator.Generate();

        Assert.Equal(first, second);
    }

    private static int FirstDifference(byte[] expected, byte[] actual)
    {
        var length = Math.Min(expected.Length, actual.Length);
        for (var i = 0; i < length; i++)
        {
            if (expected[i] != actual[i])
            {
                return i;
            }
        }

        return expected.Length == actual.Length ? -1 : length;
    }
}
