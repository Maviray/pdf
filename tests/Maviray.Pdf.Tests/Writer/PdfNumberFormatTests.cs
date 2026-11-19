using System.Globalization;
using System.Text;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

public sealed class PdfNumberFormatTests
{
    [Theory]
    [InlineData(0.0, "0")]
    [InlineData(1.0, "1")]
    [InlineData(-1.5, "-1.5")]
    [InlineData(0.5, "0.5")]
    [InlineData(595.2756, "595.2756")]
    [InlineData(841.88976, "841.8898")]
    [InlineData(1.23456, "1.2346")]
    [InlineData(0.00005, "0.0001")]
    [InlineData(0.00015, "0.0002")] // decimal midpoints round away from zero, whatever the binary value
    [InlineData(0.00145, "0.0015")]
    [InlineData(-0.00015, "-0.0002")]
    [InlineData(50.00005, "50.0001")] // half of 100.0001: layout produces midpoints like this
    [InlineData(0.000025, "0")]
    [InlineData(-0.00004, "0")]
    [InlineData(1e15, "1000000000000000")]
    [InlineData(123456789012.3456, "123456789012.346")] // 15 significant digits: .NET's precision for custom formats
    public void FormatsToAtMostFourDecimals(double value, string expected)
    {
        Assert.Equal(expected, Format(value));
    }

    // Its own test: xUnit treats InlineData(0.0) and InlineData(-0.0) as duplicates.
    [Fact]
    public void NegativeZeroWritesZero()
    {
        var value = -0.0;
        Assert.True(double.IsNegative(value));

        Assert.Equal("0", Format(value));
    }

    [Fact]
    public void LargestAllowedValueHasNoExponent()
    {
        double value = float.MaxValue;

        var result = Format(value);

        Assert.DoesNotContain("E", result, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(39, result.Length);
        var parsed = double.Parse(result, CultureInfo.InvariantCulture);
        Assert.True(Math.Abs(parsed - value) / value < 1e-14, $"{result} is too far from {value:R}");
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(3.5e38)]
    [InlineData(-3.5e38)]
    public void RefusesValuesAPdfCannotHold(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Format(value));
    }

    [Fact]
    public void IgnoresTheCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal(",", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);

            Assert.Equal("1.5", Format(1.5));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void OutputWritesTheFormattedNumber()
    {
        var stream = new MemoryStream();
        using var output = new PdfOutput(stream);

        output.WriteNumber(-12.25);
        output.Flush();

        Assert.Equal("-12.25", Encoding.Latin1.GetString(stream.ToArray()));
    }

    private static string Format(double value)
    {
        Span<byte> destination = stackalloc byte[PdfNumberFormat.MaxLength];
        var length = PdfNumberFormat.Format(value, destination);
        return Encoding.ASCII.GetString(destination[..length]);
    }
}
