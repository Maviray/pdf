using System.Reflection;
using Maviray.Pdf.Tests.Fonts;
using Maviray.Pdf.Tests.Validation;

namespace Maviray.Pdf.Tests.FontsDefault;

// The Maviray.Pdf.Fonts.Default package (L-15): the four Noto Sans faces, compiled in, and RegisterNotoSans().
public sealed class DefaultFontsTests
{
    private static readonly Assembly Package = typeof(DefaultFonts).Assembly;

    [Theory]
    [InlineData(400, false, "NotoSans-Regular")]
    [InlineData(700, false, "NotoSans-Bold")]
    [InlineData(400, true, "NotoSans-Italic")]
    [InlineData(700, true, "NotoSans-BoldItalic")]
    public void TheFourFacesAreRegistered(int weight, bool italic, string postScriptName)
    {
        var registry = new FontRegistry().RegisterNotoSans();

        var match = registry.Match("Noto Sans", weight, italic)!;

        Assert.Equal(["Noto Sans"], registry.Families);
        Assert.Equal((postScriptName, false), (match.Face.Font.PostScriptName, match.Substituted));
    }

    [Fact]
    public void TheResourcesAreTheCommittedFiles()
    {
        string[] styles = ["Bold", "BoldItalic", "Italic", "Regular"];

        Assert.Equal(styles.Select(style => $"NotoSans-{style}.ttf"), Package.GetManifestResourceNames().Order(StringComparer.Ordinal));
        Assert.All(styles, style =>
        {
            using var resource = Package.GetManifestResourceStream($"NotoSans-{style}.ttf")!;
            using var copy = new MemoryStream();
            resource.CopyTo(copy);
            Assert.Equal(File.ReadAllBytes(TestFonts.NotoSansPath(style)), copy.ToArray());
        });
    }

    [Fact]
    public void ItReturnsTheRegistry()
    {
        var registry = new FontRegistry();

        Assert.Same(registry, registry.RegisterNotoSans());
    }

    [Fact]
    public void ASecondCallChangesNothing()
    {
        var registry = new FontRegistry().RegisterNotoSans();

        Assert.Same(registry, registry.RegisterNotoSans());
        Assert.Equal(["Noto Sans"], registry.Families);
        Assert.Equal("NotoSans-Regular", registry.Match("Noto Sans", 400, italic: false)!.Face.Font.PostScriptName);
    }

    [Fact]
    public void CallsFromManyThreadsRegisterOnce()
    {
        var registry = new FontRegistry();

        Parallel.For(0, 8, _ => registry.RegisterNotoSans());

        Assert.Equal(["Noto Sans"], registry.Families);
        Assert.Equal("NotoSans-BoldItalic", registry.Match("Noto Sans", 700, italic: true)!.Face.Font.PostScriptName);
    }

    [Fact]
    public void ANotoSansFaceRegisteredElsewhereThrows()
    {
        var registry = new FontRegistry().Register("Noto Sans", TestFonts.NotoSansPath("Regular"));

        Assert.Throws<ArgumentException>(() => registry.RegisterNotoSans());
    }

    [Fact]
    public void ANullRegistryThrows() =>
        Assert.Equal("registry", Assert.Throws<ArgumentNullException>(() => DefaultFonts.RegisterNotoSans(null!)).ParamName);

    [Fact]
    public void TheAssemblyReferencesOnlyTheCoreAndTheRuntime()
    {
        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var references = Package.GetReferencedAssemblies().Select(reference => reference.Name!).ToList();

        Assert.Contains("Maviray.Pdf", references);
        Assert.DoesNotContain(references, name => name != "Maviray.Pdf" && !File.Exists(Path.Combine(runtimeDirectory, name + ".dll")));
    }

    [Fact]
    public void IsMarkedTrimmable() =>
        Assert.Contains(Package.GetCustomAttributes<AssemblyMetadataAttribute>(), attribute => attribute.Key == "IsTrimmable" && attribute.Value == "True");

    [Fact]
    public void ADocumentWithOnlyRegisterNotoSansPassesQpdf()
    {
        FontRegistry.Default.RegisterNotoSans();

        var path = GeneratedFiles.Save(Document.Create(document => document.Page(page =>
        {
            page.Margin(2, Unit.Centimetre);
            page.Content().Column(column =>
            {
                column.Item().Text("Grüße aus Berlin").Bold();
                column.Item().Text("İstanbul, Ağrı, Şişli").Italic();
                column.Item().Text("Счёт № 42");
            });
        })).ToBytes());

        Qpdf.Check(path);
        var fonts = Poppler.ListFonts(path);
        Assert.All(fonts, font => Assert.True(font.Embedded && font.Subset && font.Unicode, font.ToString()));
        Assert.Equal(["NotoSans-Bold", "NotoSans-Italic", "NotoSans-Regular"], fonts.Select(font => font.Name[(font.Name.IndexOf('+', StringComparison.Ordinal) + 1)..]).Order(StringComparer.Ordinal));
        Assert.Contains("İstanbul, Ağrı, Şişli", Poppler.ExtractText(path), StringComparison.Ordinal);
    }
}
