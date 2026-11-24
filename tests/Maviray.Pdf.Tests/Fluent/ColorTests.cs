using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Fluent;

public sealed class ColorTests
{
    [Theory]
    [InlineData("#1F4E79", 0x1F, 0x4E, 0x79)]
    [InlineData("#1f4e79", 0x1F, 0x4E, 0x79)]
    [InlineData("#ABC", 0xAA, 0xBB, 0xCC)]
    [InlineData("#abc", 0xAA, 0xBB, 0xCC)]
    public void FromHexReadsBothForms(string hex, byte r, byte g, byte b)
    {
        var color = Color.FromHex(hex);

        Assert.Equal((r, g, b), (color.R, color.G, color.B));
    }

    [Theory]
    [InlineData("1F4E79")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#GGGGGG")]
    [InlineData("# 1F4E7")]
    [InlineData("")]
    public void FromHexRefusesMalformedText(string hex)
    {
        var error = Assert.Throws<ArgumentException>(() => Color.FromHex(hex));

        Assert.Equal("hex", error.ParamName);
    }

    [Fact]
    public void FromHexRefusesNull() => Assert.Throws<ArgumentNullException>(() => Color.FromHex(null!));

    [Fact]
    public void TheComponentsMapOntoRgbColor()
    {
        var rgb = Color.FromRgb(255, 0, 51).ToRgb();

        Assert.Equal(new RgbColor(1, 0, 0.2), rgb);
    }

    [Fact]
    public void BlackAndWhite()
    {
        Assert.Equal(Color.FromRgb(0, 0, 0), Color.Black);
        Assert.Equal(Color.FromRgb(255, 255, 255), Color.White);
        Assert.NotEqual(Color.Black, Color.White);
    }

    [Fact]
    public void FontWeightsRunFromThinToBlack()
    {
        Assert.Equal(100, (int)FontWeight.Thin);
        Assert.Equal(400, (int)FontWeight.Normal);
        Assert.Equal(700, (int)FontWeight.Bold);
        Assert.Equal(900, (int)FontWeight.Black);
    }
}
