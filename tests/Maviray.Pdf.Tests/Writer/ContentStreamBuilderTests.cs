using System.Text;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

public sealed class ContentStreamBuilderTests
{
    private static readonly RgbColor Red = new(1, 0, 0);
    private static readonly RgbColor Blue = new(0, 0, 1);

    [Theory]
    [InlineData("q Q", "q\nQ\n")]
    [InlineData("cm", "1 0 0 1 72 72 cm\n")]
    [InlineData("w", "0.5 w\n")]
    [InlineData("rg", "0.2 0.4 0.8 rg\n")]
    [InlineData("RG", "1 0 0 RG\n")]
    [InlineData("re f", "72 600 200 100 re\nf\n")]
    [InlineData("re S", "72 600 200 100 re\nS\n")]
    [InlineData("re B", "72 600 200 100 re\nB\n")]
    public void WritesEachOperator(string operators, string expected)
    {
        Assert.Equal(expected, Written(builder => _ = operators switch
        {
            "q Q" => builder.SaveState().RestoreState(),
            "cm" => builder.Transform(1, 0, 0, 1, 72, 72),
            "w" => builder.SetLineWidth(0.5),
            "rg" => builder.SetFillColor(new RgbColor(0.2, 0.4, 0.8)),
            "RG" => builder.SetStrokeColor(Red),
            "re f" => builder.Rectangle(72, 600, 200, 100).Fill(),
            "re S" => builder.Rectangle(72, 600, 200, 100).Stroke(),
            "re B" => builder.Rectangle(72, 600, 200, 100).FillAndStroke(),
            _ => throw new ArgumentOutOfRangeException(nameof(operators)),
        }));
    }

    [Fact]
    public void WritesTheFirstSettingEvenIfItIsTheDefault()
    {
        Assert.Equal("1 w\n", Written(builder => builder.SetLineWidth(1)));
        Assert.Equal("0 0 0 rg\n", Written(builder => builder.SetFillColor(new RgbColor(0, 0, 0))));
    }

    [Fact]
    public void SkipsASettingThatIsAlreadyCurrent()
    {
        Assert.Equal("1 0 0 rg\n", Written(builder => builder.SetFillColor(Red).SetFillColor(Red)));
        Assert.Equal("2 w\n", Written(builder => builder.SetLineWidth(2).SetLineWidth(2)));
        Assert.Equal("0 0 1 RG\n", Written(builder => builder.SetStrokeColor(Blue).SetStrokeColor(Blue)));
        Assert.Equal("1 0 0 rg\n1 0 0 RG\n", Written(builder => builder.SetFillColor(Red).SetStrokeColor(Red)));
    }

    [Fact]
    public void RestoreStateRestoresTheTrackedValues()
    {
        var written = Written(builder => builder
            .SetFillColor(Red)
            .SaveState()
            .SetFillColor(Blue)
            .RestoreState()
            .SetFillColor(Red)
            .SetFillColor(Blue));

        Assert.Equal("1 0 0 rg\nq\n0 0 1 rg\nQ\n0 0 1 rg\n", written);
    }

    [Fact]
    public void AllowsSeveralRectanglesInOnePath()
    {
        Assert.Equal(
            "0 0 1 1 re\n2 2 1 1 re\nf\n",
            Written(builder => builder.Rectangle(0, 0, 1, 1).Rectangle(2, 2, 1, 1).Fill()));
    }

    [Fact]
    public void RestoreWithoutSaveThrows()
    {
        using var builder = new ContentStreamBuilder();

        Assert.Throws<InvalidOperationException>(builder.RestoreState);
    }

    [Theory]
    [InlineData("f")]
    [InlineData("S")]
    [InlineData("B")]
    public void PaintingWithoutAPathThrows(string paint)
    {
        using var builder = new ContentStreamBuilder();

        Assert.Throws<InvalidOperationException>(() => Paint(builder, paint));
    }

    [Theory]
    [InlineData("rg")]
    [InlineData("RG")]
    [InlineData("w")]
    [InlineData("cm")]
    [InlineData("q")]
    [InlineData("Q")]
    public void ChangingStateInsideAPathThrows(string change)
    {
        using var builder = new ContentStreamBuilder();
        builder.SaveState().Rectangle(0, 0, 1, 1);

        Assert.Throws<InvalidOperationException>(() => _ = change switch
        {
            "rg" => builder.SetFillColor(Red),
            "RG" => builder.SetStrokeColor(Red),
            "w" => builder.SetLineWidth(2),
            "cm" => builder.Transform(1, 0, 0, 1, 0, 0),
            "q" => builder.SaveState(),
            "Q" => builder.RestoreState(),
            _ => throw new ArgumentOutOfRangeException(nameof(change)),
        });
    }

    [Fact]
    public void FinishingWithAnOpenPathOrStateThrows()
    {
        using var withPath = new ContentStreamBuilder();
        withPath.Rectangle(0, 0, 1, 1);
        Assert.Throws<InvalidOperationException>(() => withPath.Finish());

        using var withState = new ContentStreamBuilder();
        withState.SaveState();
        Assert.Throws<InvalidOperationException>(() => withState.Finish());
    }

    [Fact]
    public void UsingTheBuilderAfterFinishThrows()
    {
        using var builder = new ContentStreamBuilder();
        builder.SetLineWidth(1).Finish();

        Assert.Throws<InvalidOperationException>(() => builder.SetLineWidth(2));
        Assert.Throws<InvalidOperationException>(() => builder.Finish());
    }

    // Every operand is checked before anything is written, so a call that throws leaves the stream as it was.
    [Theory]
    [InlineData("re", "height")]
    [InlineData("cm", "e")]
    [InlineData("w", "width")]
    public void AThrowingCallWritesNothing(string call, string parameter)
    {
        using var builder = new ContentStreamBuilder();
        builder.SetLineWidth(1);

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => _ = call switch
        {
            "re" => builder.Rectangle(1, 2, 3, double.NaN),
            "cm" => builder.Transform(1, 0, 0, 1, double.PositiveInfinity, 0),
            "w" => builder.SetLineWidth(double.NaN),
            _ => throw new ArgumentOutOfRangeException(nameof(call)),
        });

        Assert.Equal(parameter, error.ParamName);
        Assert.Equal("1 w\n", Encoding.Latin1.GetString(builder.Finish()));
    }

    [Fact]
    public void AnInvalidLineWidthIsRefusedEveryTime()
    {
        using var builder = new ContentStreamBuilder();

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.SetLineWidth(double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.SetLineWidth(double.PositiveInfinity));
    }

    [Theory]
    [InlineData(-2.0)]
    [InlineData(-0.0001)]
    public void RefusesANegativeLineWidth(double width)
    {
        using var builder = new ContentStreamBuilder();

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.SetLineWidth(width));
    }

    [Fact]
    public void AcceptsAZeroLineWidth() => Assert.Equal("0 w\n", Written(builder => builder.SetLineWidth(0)));

    [Fact]
    public void IsEmptyUntilSomethingIsWritten()
    {
        using var builder = new ContentStreamBuilder();
        Assert.True(builder.IsEmpty);

        builder.SetLineWidth(1);

        Assert.False(builder.IsEmpty);
    }

    private static string Written(Action<ContentStreamBuilder> build)
    {
        using var builder = new ContentStreamBuilder();
        build(builder);
        return Encoding.Latin1.GetString(builder.Finish());
    }

    private static ContentStreamBuilder Paint(ContentStreamBuilder builder, string paint) => paint switch
    {
        "f" => builder.Fill(),
        "S" => builder.Stroke(),
        "B" => builder.FillAndStroke(),
        _ => throw new ArgumentOutOfRangeException(nameof(paint)),
    };
}
