using Maviray.Pdf.Tests.Fonts;

namespace Maviray.Pdf.Tests.Fluent;

// The font registry (spec L-09), with the four Noto Sans files.
public sealed class FontRegistryTests
{
    private static readonly byte[] Regular = File.ReadAllBytes(TestFonts.NotoSansPath("Regular"));
    private static readonly byte[] Bold = File.ReadAllBytes(TestFonts.NotoSansPath("Bold"));
    private static readonly byte[] Italic = File.ReadAllBytes(TestFonts.NotoSansPath("Italic"));

    [Fact]
    public void AFaceTakesItsWeightAndStyleFromTheFont()
    {
        var registry = new FontRegistry().Register("Noto Sans", Regular).Register("Noto Sans", Bold).Register("Noto Sans", Italic);

        var bold = registry.Match("Noto Sans", 700, italic: false)!;
        var italic = registry.Match("Noto Sans", 400, italic: true)!;

        Assert.Equal((700, false, false, "NotoSans-Bold"), (bold.Face.Weight, bold.Face.Italic, bold.Substituted, bold.Face.Font.PostScriptName));
        Assert.Equal((400, true, false), (italic.Face.Weight, italic.Face.Italic, italic.Substituted));
    }

    [Fact]
    public void OverridesWin()
    {
        var match = new FontRegistry().Register("Mine", Regular, FontWeight.Light, italic: true).Match("Mine", 300, italic: true)!;

        Assert.Equal((300, true, false), (match.Face.Weight, match.Face.Italic, match.Substituted));
    }

    [Fact]
    public void FamiliesIgnoreCaseAndKeepTheirSpelling()
    {
        var registry = new FontRegistry().Register("Noto Sans", Regular);

        Assert.NotNull(registry.Match("NOTO SANS", 400, italic: false));
        Assert.Equal(["Noto Sans"], registry.Families);
    }

    [Fact]
    public void ASecondFaceWithTheSameWeightAndStyleThrows()
    {
        var registry = new FontRegistry().Register("Noto Sans", Regular);

        Assert.Throws<ArgumentException>(() => registry.Register("noto sans", Regular));
    }

    [Fact]
    public void ItalicFallsBackToUprightAndIsSubstituted()
    {
        var match = new FontRegistry().Register("Noto Sans", Regular).Register("Noto Sans", Bold).Match("Noto Sans", 400, italic: true)!;

        Assert.Equal((400, false, true), (match.Face.Weight, match.Face.Italic, match.Substituted));
    }

    [Fact]
    public void UprightFallsBackToItalicAndIsSubstituted()
    {
        var match = new FontRegistry().Register("Noto Sans", Italic).Match("Noto Sans", 400, italic: false)!;

        Assert.Equal((true, true), (match.Face.Italic, match.Substituted));
    }

    [Fact]
    public void AnotherWeightIsSubstituted()
    {
        var match = new FontRegistry().Register("Noto Sans", Regular).Match("Noto Sans", 700, italic: false)!;

        Assert.Equal((400, true), (match.Face.Weight, match.Substituted));
    }

    [Fact]
    public void AnUnknownFamilyMatchesNothing() => Assert.Null(new FontRegistry().Register("Noto Sans", Regular).Match("Arial", 400, italic: false));

    [Fact]
    public void AFontThatCantBeLoadedThrowsAtRegistration() =>
        Assert.Throws<InvalidDataException>(() => new FontRegistry().Register("Broken", new byte[] { 1, 2, 3 }));

    [Fact]
    public void StreamAndPathRegisterTheSameFace()
    {
        using var stream = new MemoryStream(Regular);
        var fromStream = new FontRegistry().Register("A", stream);
        var fromPath = new FontRegistry().Register("A", TestFonts.NotoSansPath("Regular"));

        Assert.True(stream.CanRead, "The registry disposed the caller's stream.");
        Assert.Equal("NotoSans-Regular", fromStream.Match("A", 400, false)!.Face.Font.PostScriptName);
        Assert.Equal("NotoSans-Regular", fromPath.Match("A", 400, false)!.Face.Font.PostScriptName);
    }

    [Fact]
    public void RegistrationIsSafeFromManyThreads()
    {
        var registry = new FontRegistry();

        Parallel.For(0, 8, i =>
        {
            registry.Register($"Family {i}", Regular);
            Assert.NotNull(registry.Match($"Family {i}", 400, italic: false));
        });

        Assert.Equal(8, registry.Families.Count);
    }

    [Fact]
    public void DefaultIsOneRegistry() => Assert.Same(FontRegistry.Default, FontRegistry.Default);

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void RefusesABlankFamily(string family) => Assert.Throws<ArgumentException>(() => new FontRegistry().Register(family, Regular));

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void RefusesAWeightOutOfRange(int weight) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new FontRegistry().Register("A", Regular, (FontWeight)weight));

    [Fact]
    public void RefusesNullData() => Assert.Throws<ArgumentNullException>(() => new FontRegistry().Register("A", (byte[])null!));
}
