using Maviray.Pdf.Tests.Text;
using Maviray.Pdf.Tests.Validation;

namespace Maviray.Pdf.Tests.Fluent;

// The README's example under "Getting started" lives here, between the markers, so it compiles and runs. The test checks
// that the README shows exactly these lines, and that the document they write passes qpdf.
public sealed class ReadmeExampleTests
{
    private const string ThisFile = "tests/Maviray.Pdf.Tests/Fluent/ReadmeExampleTests.cs";
    private const string BeginMarker = "// README begin";
    private const string EndMarker = "// README end";

    [Fact]
    public void TheReadmeExampleRuns()
    {
        var example = Example();
        var shown = ReadmeBlock();
        var difference = Enumerable.Range(0, Math.Max(example.Count, shown.Count))
            .FirstOrDefault(i => !string.Equals(At(example, i), At(shown, i), StringComparison.Ordinal), -1);
        Assert.True(
            difference < 0,
            $"README.md's example differs from {ThisFile} at its line {difference + 1}:\n  README: {At(shown, difference)}\n  test:   {At(example, difference)}");

        var path = GeneratedFiles.Save([]);
        SaveInvoice(path);

        Qpdf.Check(path);
        Assert.Contains("Invoice 2026-0042", Poppler.ExtractText(path), StringComparison.Ordinal);
    }

    // README begin
    static void SaveInvoice(string path)
    {
        FontRegistry.Default.RegisterNotoSans();

        (string Item, string Amount)[] lines =
        [
            ("Server maintenance, November", "€1,200.00"),
            ("Backup storage, 2 TB", "€80.00"),
            ("Support, 6 hours × €95.00", "€570.00"),
        ];

        Document.Create(document =>
        {
            document.Metadata(metadata => metadata.Title("Invoice 2026-0042").Author("Maviray GmbH").Language("en-GB"));
            document.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(20, Unit.Millimetre);
                page.DefaultTextStyle(style => style.FontSize(10));

                page.Header().PaddingBottom(12).Text("Invoice 2026-0042").FontSize(20).Bold();

                page.Content().Column(column =>
                {
                    column.Spacing(6);
                    column.Item().Text("Maviray GmbH · Musterstraße 1 · 10115 Berlin").FontColor(Color.FromHex("#666666"));
                    column.Item().PaddingTop(12).BorderBottom(1, Color.Black).Row(row =>
                    {
                        row.Flex().Text("Item").Bold();
                        row.Fixed(100).AlignRight().Text("Amount").Bold();
                    });
                    foreach (var (item, amount) in lines)
                    {
                        column.Item().Row(row =>
                        {
                            row.Flex().Text(item);
                            row.Fixed(100).AlignRight().Text(amount);
                        });
                    }

                    column.Item().BorderTop(1, Color.Black).AlignRight().Text("Total: €1,850.00").Bold();
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.PageNumber();
                    text.Span(" of ");
                    text.PageCount();
                });
            });
        }).Save(path);
    }
    // README end

    private static string At(List<string> lines, int index) => index >= 0 && index < lines.Count ? lines[index] : "(no line)";

    // The lines between the markers in this file, without their common indentation.
    private static List<string> Example()
    {
        var lines = Lines(RepositoryFiles.PathOf(ThisFile));
        var begin = lines.FindIndex(line => line.Trim() == BeginMarker);
        var end = lines.FindIndex(line => line.Trim() == EndMarker);
        var body = lines[(begin + 1)..end];
        var indent = body.Where(line => line.Length > 0).Min(line => line.Length - line.TrimStart(' ').Length);
        return body.ConvertAll(line => line.Length > 0 ? line[indent..] : line);
    }

    // The first C# block in README.md's "Getting started" section.
    private static List<string> ReadmeBlock()
    {
        var lines = Lines(RepositoryFiles.PathOf("README.md"));
        var section = lines.FindIndex(line => line == "## Getting started");
        var next = lines.FindIndex(section + 1, line => line.StartsWith("## ", StringComparison.Ordinal));
        var open = lines.FindIndex(section + 1, (next < 0 ? lines.Count : next) - section - 1, line => line == "```csharp");
        Assert.True(open >= 0, "README.md has no ```csharp block under \"## Getting started\".");
        var close = lines.FindIndex(open + 1, line => line == "```");
        return lines[(open + 1)..close];
    }

    private static List<string> Lines(string path) => [.. File.ReadAllText(path).ReplaceLineEndings("\n").Split('\n')];
}
