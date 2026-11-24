using Maviray.Pdf.Text;

namespace Maviray.Pdf.Tests.Text;

// A style holds the values that decide geometry, and refuses invalid ones by name (spec T-14).
public sealed class SpanStyleTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RefusesASizeThatIsntPositiveAndFinite(double size)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => new SpanStyle(LayoutTestFont.Font, size));

        Assert.Equal("size", error.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RefusesALineHeightThatIsntPositiveAndFinite(double lineHeight)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => new SpanStyle(LayoutTestFont.Font, 10, lineHeight));

        Assert.Equal("lineHeight", error.ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    public void RefusesALetterSpacingThatIsntFinite(double letterSpacing)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => new SpanStyle(LayoutTestFont.Font, 10, null, letterSpacing));

        Assert.Equal("letterSpacing", error.ParamName);
    }

    [Fact]
    public void RefusesNoFont()
    {
        Assert.Throws<ArgumentNullException>(() => new SpanStyle(null!, 10));
    }

    [Fact]
    public void KeepsItsValues()
    {
        var style = new SpanStyle(LayoutTestFont.Font, 12, 1.5, -0.25, underline: true, strikethrough: true);

        Assert.Same(LayoutTestFont.Font, style.Font);
        Assert.Equal(12, style.Size);
        Assert.Equal(1.5, style.LineHeight);
        Assert.Equal(-0.25, style.LetterSpacing);
        Assert.True(style.Underline);
        Assert.True(style.Strikethrough);
    }

    [Fact]
    public void ParagraphsRefuseWhatTheyCantHold()
    {
        var span = new TextSpan("a", LayoutTestFont.Style());

        Assert.Throws<ArgumentNullException>(() => new TextSpan(null!, LayoutTestFont.Style()));
        Assert.Throws<ArgumentNullException>(() => new TextSpan("a", null!));
        Assert.Throws<ArgumentNullException>(() => new Paragraph(null!));
        Assert.Throws<ArgumentException>(() => new Paragraph([span, null!]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Paragraph([span], (TextAlignment)7));
    }

    [Fact]
    public void ParagraphsCopyTheirSpans()
    {
        var spans = new List<TextSpan> { new("a", LayoutTestFont.Style()) };
        var paragraph = new Paragraph(spans, TextAlignment.Center);
        spans.Clear();

        Assert.Single(paragraph.Spans);
        Assert.Equal(TextAlignment.Center, paragraph.Alignment);
    }
}
