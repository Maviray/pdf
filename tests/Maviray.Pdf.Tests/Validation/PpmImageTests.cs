namespace Maviray.Pdf.Tests.Validation;

public sealed class PpmImageTests
{
    [Fact]
    public void ParsesABinaryP6Image()
    {
        byte[] bytes = [.. "P6\n2 1\n255\n"u8, 0xFF, 0x00, 0x00, 0x00, 0x00, 0xFF];

        var image = PpmImage.Parse(bytes);

        Assert.Equal(2, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Equal(((byte)255, (byte)0, (byte)0), image.GetPixel(0, 0));
        Assert.Equal(((byte)0, (byte)0, (byte)255), image.GetPixel(1, 0));
    }

    [Fact]
    public void RefusesOtherFormats()
    {
        Assert.Throws<FormatException>(() => PpmImage.Parse("P3\n1 1\n255\n0 0 0\n"u8.ToArray()));
    }
}
