using Maviray.Pdf.Text;

namespace Maviray.Pdf.Tests.Text;

// The cluster cases of spec T-05, each built from its code points so the test source stays ASCII.
public sealed class GraphemeClustersTests
{
    [Theory]
    [InlineData(new[] { 0x0065, 0x0301 })] // e and a combining acute
    [InlineData(new[] { 0x043E, 0x0301 })] // a Cyrillic vowel with a stress mark
    [InlineData(new[] { 0x0430, 0x0301, 0x0308 })] // a base with two marks
    [InlineData(new[] { 0x000D, 0x000A })] // CR LF
    [InlineData(new[] { 0x1F1E9, 0x1F1EA })] // a regional-indicator pair, a flag
    [InlineData(new[] { 0x1F468, 0x200D, 0x1F469, 0x200D, 0x1F467 })] // an emoji ZWJ sequence
    public void IsOneCluster(int[] codePoints)
    {
        var text = Text(codePoints);

        Assert.Equal(Starts(text.Length, 0), GraphemeClusters.FindStarts(text));
    }

    [Fact]
    public void ThreeRegionalIndicatorsAreTwoClusters()
    {
        var text = Text([0x1F1E9, 0x1F1EA, 0x1F1EB]);

        Assert.Equal(Starts(text.Length, 0, 4), GraphemeClusters.FindStarts(text));
    }

    [Fact]
    public void LettersAreClustersOfTheirOwn()
    {
        Assert.Equal(Starts(2, 0, 1), GraphemeClusters.FindStarts("ab"));
        Assert.Equal(Starts(0), GraphemeClusters.FindStarts(""));
    }

    private static string Text(int[] codePoints) => string.Concat(codePoints.Select(char.ConvertFromUtf32));

    /// <summary>The expected array: true at each given start and at the end.</summary>
    private static bool[] Starts(int length, params int[] starts)
    {
        var expected = new bool[length + 1];
        foreach (var start in starts)
        {
            expected[start] = true;
        }

        expected[length] = true;
        return expected;
    }
}
