using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Maviray.Pdf.Text;

namespace Maviray.Pdf.Tests.Text;

// Expected values from the UCD 16.0.0 files, as the P1.5 plan records them, and from the Unicode Standard 3.11 and 3.12.
public sealed class TextNormalizerTests
{
    [Theory]
    [InlineData("")]
    [InlineData("Hello")]
    [InlineData("\u00C4pfel \u00FCber Stra\u00DFe")]
    [InlineData("q\u0308")] // U+0308 may compose backwards, but there is no q with diaeresis
    [InlineData("\u0915\u093C")] // U+0958 is excluded from composition
    [InlineData("\u0995\u09BE")] // U+09BE is Maybe and starts no composite with U+0995
    [InlineData("\u0301a")] // a leading mark has no starter to compose with
    [InlineData("\uAC01")]
    [InlineData("\U0001F600")]
    public void NfcTextIsReturnedAsTheSameInstance(string text)
    {
        Assert.Same(text, TextNormalizer.ToNfc(text));
    }

    // Measured in optimized builds only. Without JIT optimization, as in a Debug build, the JIT doesn't expand
    // RuntimeHelpers.CreateSpan, so each read of a ushort, int or ulong table allocates a field handle. CI runs this
    // test in Release, which is also what the package ships.
    [Fact]
    public void NfcTextAllocatesNothing()
    {
        var debuggable = typeof(TextNormalizer).Assembly.GetCustomAttribute<DebuggableAttribute>();
        Assert.SkipWhen(
            debuggable?.IsJITOptimizerDisabled == true,
            "Maviray.Pdf was built without optimization; run the tests with -c Release to measure allocations.");

        const string german = "\u00C4pfel \u00FCber Stra\u00DFe";
        const string maybe = "q\u0308";
        TextNormalizer.ToNfc(german);
        TextNormalizer.ToNfc(maybe);

        var before = GC.GetAllocatedBytesForCurrentThread();
        TextNormalizer.ToNfc(german);
        TextNormalizer.ToNfc(maybe);
        var after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(before, after);
    }

    [Theory]
    [InlineData("A\u030A", "\u00C5")]
    [InlineData("C\u0301\u0327", "\u1E08")] // reordered by class, then composed twice
    [InlineData("a\u0302\u0323", "\u1EAD")]
    [InlineData("a\u0323\u0302", "\u1EAD")]
    [InlineData("Ko\u0308ln", "K\u00F6ln")]
    [InlineData("\u212B", "\u00C5")] // singleton
    [InlineData("\u2126", "\u03A9")] // singleton
    [InlineData("\u0958", "\u0915\u093C")] // listed exclusion
    [InlineData("\u2ADC", "\u2ADD\u0338")] // listed exclusion, post composition version
    [InlineData("\u0344", "\u0308\u0301")] // non-starter decomposition
    [InlineData("\u0F73", "\u0F71\u0F72")] // non-starter decomposition
    [InlineData("\U00011099\U000110BA", "\U0001109A")]
    [InlineData("\U0001D15E", "\U0001D157\U0001D165")]
    [InlineData("\U0001138B\U000113C7", "\U0001138E\U000113B8")] // a chained composite, new in Unicode 16.0
    [InlineData("\U00016D63\U00016D68", "\U00016D6A")] // chained twice: U+16D69, then U+16D6A
    [InlineData("\u1100\u1161\u11A8", "\uAC01")]
    [InlineData("\uAC00\u11A8", "\uAC01")]
    public void ComposesAndReorders(string text, string expected)
    {
        Assert.Equal(expected, TextNormalizer.ToNfc(text));
    }

    // Written as UTF-16 code units in hex, because a lone surrogate can't survive being serialized as theory data.
    [Theory]
    [InlineData("0061 D800 0062", "0061 FFFD 0062")]
    [InlineData("DC00", "FFFD")]
    [InlineData("0078 D83D", "0078 FFFD")]
    [InlineData("DE00 D83D", "FFFD FFFD")]
    [InlineData("0065 D800 0301", "0065 FFFD 0301")] // U+FFFD is a starter, so the e and its accent don't compose
    [InlineData("D83D DE00 DC00", "D83D DE00 FFFD")]
    public void ReplacesUnpairedSurrogates(string text, string expected)
    {
        Assert.Equal(Units(expected), TextNormalizer.ToNfc(Units(text)));
    }

    [Fact]
    public void KeepsTheTextBeforeTheFirstChange()
    {
        var prefix = string.Concat(Enumerable.Repeat("ab ", 10_000));

        Assert.Equal(prefix + "\u00E9", TextNormalizer.ToNfc(prefix + "e\u0301"));
    }

    // Each input decomposes to far more code points than the buffer the normalizer rents first, so the buffer has to
    // grow, and what was decomposed before the growth must survive it.
    [Fact]
    public void GrowsItsBufferForLongExpansions()
    {
        // A leading mark sends everything through the slow path; each syllable decomposes to three jamo and recomposes.
        var syllables = char.ConvertFromUtf32(0x0301) + Repeat(0xD4DB, 1_000);
        Assert.Same(syllables, TextNormalizer.ToNfc(syllables));

        // U+0344 is excluded from composition, and its two marks of class 230 stay in order.
        Assert.Equal(Repeat(0x0308, 0x0301, 1_000), TextNormalizer.ToNfc(Repeat(0x0344, 1_000)));

        // Ordinary text: one decomposed letter, then precomposed letters, which the slow path decomposes and recomposes.
        var decomposed = "Ko" + char.ConvertFromUtf32(0x0308) + "ln " + Repeat(0x00E4, 1_000);
        var composed = "K" + char.ConvertFromUtf32(0x00F6) + "ln " + Repeat(0x00E4, 1_000);
        Assert.Equal(composed, TextNormalizer.ToNfc(decomposed));
    }

    // Marks of two classes, alternating: an insertion sort would take about 10^10 steps here.
    [Fact(Timeout = 10_000)]
    public async Task LongRunsOfMarksStayFast()
    {
        var text = "a" + string.Concat(Enumerable.Repeat("\u0301\u0323", 100_000));
        var expected = "\u1EA1" + new string('\u0323', 99_999) + new string('\u0301', 100_000);

        var actual = await Task.Run(() => TextNormalizer.ToNfc(text), TestContext.Current.CancellationToken);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RefusesNull()
    {
        Assert.Throws<ArgumentNullException>(() => TextNormalizer.ToNfc(null!));
    }

    private static string Repeat(int codePoint, int count) =>
        string.Concat(Enumerable.Repeat(char.ConvertFromUtf32(codePoint), count));

    private static string Repeat(int first, int second, int count) =>
        string.Concat(Enumerable.Repeat(char.ConvertFromUtf32(first) + char.ConvertFromUtf32(second), count));

    private static string Units(string hex) =>
        string.Concat(hex.Split(' ').Select(u => (char)ushort.Parse(u, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)));
}
