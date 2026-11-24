using Maviray.Pdf.Elements;

namespace Maviray.Pdf.Tests.Fluent;

// Text styles (spec L-08): immutable, inherited where not set, and resolved from the span down to the built-in default.
public sealed class TextStyleTests
{
    [Fact]
    public void EachMethodSetsOneValueAndReturnsANewStyle()
    {
        var plain = new TextStyle();
        var styled = plain.FontFamily("Inter").FontSize(14).Bold().Italic().FontColor(Color.White).Underline().Strikethrough().LetterSpacing(0.05).LineHeight(1.4);

        Assert.NotSame(plain, styled);
        Assert.Equal(ResolvedTextStyle.BuiltIn, ResolvedTextStyle.Resolve(plain, plain, plain));
        Assert.Equal(
            new ResolvedTextStyle("Inter", 14, 700, true, Color.White, true, true, 0.05, 1.4),
            ResolvedTextStyle.Resolve(styled, new TextStyle(), new TextStyle()));
    }

    [Fact]
    public void TheSpanWinsOverTheElementOverTheInheritedOverTheBuiltIn()
    {
        var span = new TextStyle().FontSize(20);
        var element = new TextStyle().FontSize(15).FontFamily("Element");
        var inherited = new TextStyle().FontSize(10).FontFamily("Inherited").Weight(FontWeight.Light);

        var resolved = ResolvedTextStyle.Resolve(span, element, inherited);

        Assert.Equal((20.0, "Element", 300), (resolved.Size, resolved.Family, resolved.Weight));
        Assert.Equal(ResolvedTextStyle.BuiltIn.Color, resolved.Color);
    }

    [Fact]
    public void ANearerDefaultWinsOverAFartherOne()
    {
        var page = new TextStyle().FontSize(10).FontColor(Color.FromHex("#1F4E79"));
        var container = new TextStyle().FontSize(11);

        var inherited = container.Over(page);

        var resolved = ResolvedTextStyle.Resolve(new TextStyle(), new TextStyle(), inherited);
        Assert.Equal((11.0, Color.FromHex("#1F4E79")), (resolved.Size, resolved.Color));
    }

    [Fact]
    public void ASpanCanUndoAnInheritedDecorationAndStyle()
    {
        var inherited = new TextStyle().Underline().Strikethrough().Italic();

        var resolved = ResolvedTextStyle.Resolve(new TextStyle().Underline(false).Strikethrough(false).Italic(false), new TextStyle(), inherited);

        Assert.Equal((false, false, false), (resolved.Underline, resolved.Strikethrough, resolved.Italic));
    }

    [Fact]
    public void TheBuiltInDefault() =>
        Assert.Equal(new ResolvedTextStyle("Noto Sans", 12, 400, false, Color.Black, false, false, 0, null), ResolvedTextStyle.BuiltIn);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000.5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RefusesABadSize(double size) => Assert.Throws<ArgumentOutOfRangeException>(() => new TextStyle().FontSize(size));

    [Fact]
    public void AcceptsTheLargestSize() => Assert.Equal(1000, ResolvedTextStyle.Resolve(new TextStyle().FontSize(1000), new TextStyle(), new TextStyle()).Size);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RefusesABadLineHeight(double multiple) => Assert.Throws<ArgumentOutOfRangeException>(() => new TextStyle().LineHeight(multiple));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RefusesABadLetterSpacing(double em) => Assert.Throws<ArgumentOutOfRangeException>(() => new TextStyle().LetterSpacing(em));

    [Fact]
    public void AcceptsANegativeLetterSpacing() =>
        Assert.Equal(-0.02, ResolvedTextStyle.Resolve(new TextStyle().LetterSpacing(-0.02), new TextStyle(), new TextStyle()).LetterSpacing);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RefusesABlankFamily(string family) => Assert.Throws<ArgumentException>(() => new TextStyle().FontFamily(family));

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void RefusesAWeightOutOfRange(int weight) => Assert.Throws<ArgumentOutOfRangeException>(() => new TextStyle().Weight((FontWeight)weight));
}
