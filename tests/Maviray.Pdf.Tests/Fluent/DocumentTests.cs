using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Maviray.Pdf.Tests.Fonts;
using Maviray.Pdf.Tests.Validation;

namespace Maviray.Pdf.Tests.Fluent;

// Documents, pages, settings and metadata (L-02, L-11, L-12, L-13), through the public API only.
public sealed class DocumentTests
{
    private static readonly FontRegistry Fonts = new FontRegistry()
        .Register("Noto Sans", File.ReadAllBytes(TestFonts.NotoSansPath("Regular")))
        .Register("Noto Sans", File.ReadAllBytes(TestFonts.NotoSansPath("Bold")))
        .Register("Noto Sans", File.ReadAllBytes(TestFonts.NotoSansPath("Italic")))
        .Register("Noto Sans", File.ReadAllBytes(TestFonts.NotoSansPath("BoldItalic")));

    [Fact]
    public void SaveToAStreamLeavesItOpen()
    {
        using var stream = new MemoryStream();

        Hello().Save(stream);

        Assert.True(stream.CanWrite, "The document disposed the caller's stream.");
        Assert.StartsWith("%PDF-1.7", Encoding.ASCII.GetString(stream.ToArray(), 0, 8), StringComparison.Ordinal);
    }

    [Fact]
    public void SaveToAPathMovesTheFileIntoPlace()
    {
        var directory = Directory.CreateTempSubdirectory("maviray-save-");
        try
        {
            var path = Path.Combine(directory.FullName, "rechnung.pdf");
            File.WriteAllText(path, "old");

            Hello(deterministic: true).Save(path);

            Assert.Equal(Hello(deterministic: true).ToBytes(), File.ReadAllBytes(path));
            Assert.Equal(["rechnung.pdf"], directory.GetFiles().Select(file => file.Name));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void AFailedSaveLeavesTheTargetAndNoTemporaryFile()
    {
        var directory = Directory.CreateTempSubdirectory("maviray-save-");
        try
        {
            var path = Path.Combine(directory.FullName, "rechnung.pdf");
            File.WriteAllText(path, "old");
            var tooTall = Document.Create(document =>
            {
                document.Settings(settings => settings.Fonts(Fonts));
                document.Page(page => page.Content().MinHeight(2000).Text("zu hoch"));
            });

            Assert.Throws<LayoutException>(() => tooTall.Save(path));

            Assert.Equal("old", File.ReadAllText(path));
            Assert.Equal(["rechnung.pdf"], directory.GetFiles().Select(file => file.Name));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void AFailedCleanupKeepsTheOriginalError()
    {
        var directory = Directory.CreateTempSubdirectory("maviray-save-");
        try
        {
            var path = Path.Combine(directory.FullName, "rechnung.pdf");
            var document = Document.Create(_ =>
            {
                // The temporary file exists while the callback runs. Windows can't delete a read-only file, so the
                // cleanup fails, and the caller must still see the callback's error.
                foreach (var file in directory.GetFiles("*.tmp"))
                {
                    file.Attributes |= FileAttributes.ReadOnly;
                }

                throw new InvalidOperationException("Absicht");
            });

            Assert.Equal("Absicht", Assert.Throws<InvalidOperationException>(() => document.Save(path)).Message);
        }
        finally
        {
            foreach (var file in directory.GetFiles())
            {
                file.Attributes = FileAttributes.Normal;
            }

            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void ToBytesEqualsTheStream()
    {
        using var stream = new MemoryStream();
        Hello(deterministic: true).Save(stream);

        Assert.Equal(stream.ToArray(), Hello(deterministic: true).ToBytes());
    }

    [Fact]
    public void EverySaveRunsTheCallbackAgain()
    {
        var runs = 0;
        var document = Document.Create(builder =>
        {
            runs++;
            builder.Settings(settings => settings.Fonts(Fonts));
            builder.Page(page => page.Content().Text($"Lauf {runs}"));
        });

        document.ToBytes();
        document.ToBytes();

        Assert.Equal(2, runs);
    }

    [Fact]
    public void NoPageSetsThrows() => Assert.Throws<InvalidOperationException>(() => Document.Create(_ => { }).ToBytes());

    [Fact]
    public void DeterministicSavesAreIdenticalAndOthersAreNot()
    {
        Assert.Equal(Hello(deterministic: true).ToBytes(), Hello(deterministic: true).ToBytes());
        Assert.NotEqual(Hello().ToBytes(), Hello().ToBytes());
    }

    [Fact]
    public void DeterministicModeHashesTheFileId()
    {
        var file = Hello(deterministic: true).ToBytes();
        var text = Latin1(file);
        var trailer = text.LastIndexOf("trailer\n", StringComparison.Ordinal);

        var expected = Convert.ToHexString(SHA256.HashData(file.AsSpan(0, trailer)).AsSpan(0, 16));

        Assert.Contains($"/ID [<{expected}> <{expected}>]", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCreationDateDefaultsToNowUnlessDeterministic()
    {
        var date = new DateTimeOffset(2026, 11, 23, 14, 5, 0, TimeSpan.FromHours(1));

        Assert.Contains("/CreationDate (D:", Latin1(Hello().ToBytes()), StringComparison.Ordinal);
        Assert.DoesNotContain("/CreationDate", Latin1(Hello(deterministic: true).ToBytes()), StringComparison.Ordinal);
        Assert.Contains("/CreationDate (D:20261123140500+01'00')", Latin1(Hello(deterministic: true, date: date).ToBytes()), StringComparison.Ordinal);
    }

    [Fact]
    public void MetadataReadsBack()
    {
        var path = GeneratedFiles.Save(Hello(title: "Fatura – Счёт № 42", language: "tr-TR").ToBytes());

        var info = Poppler.Info(path);

        Assert.Equal("Fatura – Счёт № 42", info["Title"]);
        Assert.Equal("Maviray GmbH", info["Author"]);
        Assert.Contains("/Lang (tr-TR)", Latin1(File.ReadAllBytes(path)), StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticsReachTheHandler()
    {
        var seen = new List<Diagnostic>();
        Document.Create(document =>
        {
            document.Settings(settings => settings.Fonts(Fonts).OnDiagnostic(seen.Add));
            document.Page(page => page.Content().Text("A " + char.ConvertFromUtf32(0x4E2D)));
        }).ToBytes();

        Assert.Equal(DiagnosticKind.MissingGlyph, Assert.Single(seen).Kind);
    }

    [Fact]
    public void ACustomRegistryIsUsed()
    {
        var custom = new FontRegistry().Register("Eigene", File.ReadAllBytes(TestFonts.NotoSansPath("Regular")));
        Document Make(bool withRegistry) => Document.Create(document =>
        {
            if (withRegistry)
            {
                document.Settings(settings => settings.Fonts(custom));
            }

            document.Page(page =>
            {
                page.DefaultTextStyle(style => style.FontFamily("Eigene"));
                page.Content().Text("Hallo");
            });
        });

        Assert.NotEmpty(Make(withRegistry: true).ToBytes());
        Assert.Contains("'Eigene' isn't registered", Assert.Throws<LayoutException>(() => Make(withRegistry: false).ToBytes()).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PageSizesMarginsAndBackgroundApply()
    {
        var path = GeneratedFiles.Save(Document.Create(document =>
        {
            document.Settings(settings => settings.Fonts(Fonts));
            document.Page(page =>
            {
                page.Size(PageSize.A5.Landscape());
                page.Margin(1, Unit.Centimetre);
                page.Background(Color.FromRgb(255, 0, 0));
                page.Content().Text("Hallo");
            });
        }).ToBytes());

        Assert.StartsWith("595.276 x 419.528 pts", Poppler.Info(path)["Page size"], StringComparison.Ordinal);
        var pixel = Assert.Single(Poppler.Render(path)).GetPixel(5, 5);
        Assert.Equal(((byte)255, (byte)0, (byte)0), pixel);
    }

    [Fact]
    public void ASlotAskedForTwiceThrows() =>
        Assert.Throws<InvalidOperationException>(() => Document.Create(document => document.Page(page =>
        {
            page.Content();
            page.Content();
        })).ToBytes());

    [Theory]
    [InlineData("de-DE")]
    [InlineData("sr-Latn-RS")]
    [InlineData("en")]
    public void ValidLanguageTagsPass(string tag) => Assert.NotEmpty(Hello(language: tag).ToBytes());

    [Theory]
    [InlineData("de_DE")]
    [InlineData("toolongpart9")]
    [InlineData("")]
    [InlineData("-de")]
    [InlineData("de-")]
    public void MalformedLanguageTagsThrow(string tag) =>
        Assert.Equal("tag", Assert.Throws<ArgumentException>(() => Document.Create(document => document.Metadata(metadata => metadata.Language(tag))).ToBytes()).ParamName);

    [Fact]
    public void BuilderArgumentsAreChecked()
    {
        void Throws<T>(Action<PageBuilder> page)
            where T : Exception => Assert.Throws<T>(() => Document.Create(document => document.Page(page)).ToBytes());

        Throws<ArgumentOutOfRangeException>(page => page.Size(0, 100));
        Throws<ArgumentOutOfRangeException>(page => page.Margin(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Document.Create(document => document.Settings(settings => settings.Compression((CompressionLevel)99))).ToBytes());
        Assert.Throws<ArgumentNullException>(() => Document.Create(document => document.Settings(settings => settings.Fonts(null!))).ToBytes());
        Assert.Throws<ArgumentNullException>(() => Document.Create(document => document.Metadata(metadata => metadata.Title(null!))).ToBytes());
        Assert.Throws<ArgumentNullException>(() => Document.Create(null!));
    }

    [Fact]
    public void AWholeDocumentPassesQpdf()
    {
        var path = GeneratedFiles.Save(Document.Create(document =>
        {
            document.Settings(settings => settings.Fonts(Fonts).Deterministic());
            document.Metadata(metadata => metadata.Title("Rechnung").Language("de-DE"));
            document.Page(page =>
            {
                page.Size(PageSize.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(style => style.FontSize(10));
                page.Header().Text("Maviray GmbH").Bold().FontSize(14);
                page.Content().Column(column =>
                {
                    column.Spacing(4);
                    for (var i = 1; i <= 80; i++)
                    {
                        column.Item().Row(row =>
                        {
                            row.Fixed(30).Text($"{i}");
                            row.Flex().Text($"Position {i}: Wartung der Server");
                            row.Auto().AlignRight().Text($"{i * 12},50 €");
                        });
                    }
                });
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Seite ");
                    text.PageNumber();
                    text.Span(" von ");
                    text.PageCount();
                });
            });
        }).ToBytes());

        Qpdf.Check(path);
        Assert.All(Poppler.ListFonts(path), font => Assert.True(font.Embedded && font.Subset && font.Unicode, font.ToString()));
        var text = Poppler.ExtractText(path);
        var pages = int.Parse(Poppler.Info(path)["Pages"], System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(pages >= 2, $"Only {pages} page.");
        Assert.All(Enumerable.Range(1, pages), n => Assert.Contains($"Seite {n} von {pages}", text, StringComparison.Ordinal));
        Assert.Contains("Position 80: Wartung der Server", text, StringComparison.Ordinal);
    }

    private static Document Hello(bool deterministic = false, DateTimeOffset? date = null, string? title = null, string? language = null) => Document.Create(document =>
    {
        document.Settings(settings => settings.Fonts(Fonts).Deterministic(deterministic));
        document.Metadata(metadata =>
        {
            metadata.Author("Maviray GmbH");
            if (date is { } value)
            {
                metadata.CreationDate(value);
            }

            if (title is not null)
            {
                metadata.Title(title);
            }

            if (language is not null)
            {
                metadata.Language(language);
            }
        });
        document.Page(page => page.Content().Text("Hallo"));
    });

    private static string Latin1(byte[] file) => Encoding.Latin1.GetString(file);
}
