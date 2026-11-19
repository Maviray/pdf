using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

public sealed class PdfScalarTests
{
    [Fact]
    public void NullWritesNull() => Assert.Equal("null", Serialized.Of(PdfNull.Instance));

    [Fact]
    public void TrueWritesTrue() => Assert.Equal("true", Serialized.Of(PdfBoolean.True));

    [Fact]
    public void FalseWritesFalse() => Assert.Equal("false", Serialized.Of(PdfBoolean.False));

    [Theory]
    [InlineData(0L, "0")]
    [InlineData(-7L, "-7")]
    [InlineData(long.MinValue, "-9223372036854775808")]
    [InlineData(long.MaxValue, "9223372036854775807")]
    public void IntegerWritesDigits(long value, string expected)
    {
        Assert.Equal(expected, Serialized.Of(new PdfInteger(value)));
    }

    [Theory]
    [InlineData(0.5, "0.5")]
    [InlineData(-0.0, "0")]
    [InlineData(72.0, "72")]
    public void RealWritesTheSharedNumberFormat(double value, string expected)
    {
        Assert.Equal(expected, Serialized.Of(new PdfReal(value)));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(3.5e38)]
    public void RealRefusesValuesAPdfCannotHold(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfReal(value));
    }

    [Fact]
    public void ReferenceWritesNumberGenerationAndR() => Assert.Equal("12 0 R", Serialized.Of(new PdfReference(12)));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ReferenceRefusesObjectNumbersBelowOne(int number)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfReference(number));
    }
}
