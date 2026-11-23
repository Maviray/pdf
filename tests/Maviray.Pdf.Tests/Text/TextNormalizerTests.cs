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
    [InlineData("Äpfel über Straße")]
    [InlineData("q̈")] // U+0308 may compose backwards, but there is no q with diaeresis
    [InlineData("क़")] // U+0958 is excluded from composition
    [InlineData("কা")] // U+09BE is Maybe and starts no composite with U+0995
    [InlineData("́a")] // a leading mark has no starter to compose with
    [InlineData("각")]
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

        const string german = "Äpfel über Straße";
        const string maybe = "q̈";
        TextNormalizer.ToNfc(german);
        TextNormalizer.ToNfc(maybe);

        var before = GC.GetAllocatedBytesForCurrentThread();
        TextNormalizer.ToNfc(german);
        TextNormalizer.ToNfc(maybe);
        var after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(before, after);
    }

    [Theory]
    [InlineData("Å", "Å")]
    [InlineData("Ḉ", "Ḉ")] // reordered by class, then composed twice
    [InlineData("ậ", "ậ")]
    [InlineData("ậ", "ậ")]
    [InlineData("Köln", "Köln")]
    [InlineData("Å", "Å")] // singleton
    [InlineData("Ω", "Ω")] // singleton
    [InlineData("क़", "क़")] // listed exclusion
    [InlineData("⫝̸", "⫝̸")] // listed exclusion, post composition version
    [InlineData("̈́", "̈́")] // non-starter decomposition
    [InlineData("ཱི", "ཱི")] // non-starter decomposition
    [InlineData("\U00011099\U000110BA", "\U0001109A")]
    [InlineData("\U0001D15E", "\U0001D157\U0001D165")]
    [InlineData("\U0001138B\U000113C7", "\U0001138E\U000113B8")] // a chained composite, new in Unicode 16.0
    [InlineData("\U00016D63\U00016D68", "\U00016D6A")] // chained twice: U+16D69, then U+16D6A
    [InlineData("각", "각")]
    [InlineData("각", "각")]
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

        Assert.Equal(prefix + "é", TextNormalizer.ToNfc(prefix + "é"));
    }

    // Marks of two classes, alternating: an insertion sort would take about 10^10 steps here.
    [Fact(Timeout = 10_000)]
    public async Task LongRunsOfMarksStayFast()
    {
        var text = "a" + string.Concat(Enumerable.Repeat("̣́", 100_000));
        var expected = "ạ" + new string('̣', 99_999) + new string('́', 100_000);

        var actual = await Task.Run(() => TextNormalizer.ToNfc(text), TestContext.Current.CancellationToken);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RefusesNull()
    {
        Assert.Throws<ArgumentNullException>(() => TextNormalizer.ToNfc(null!));
    }

    private static string Units(string hex) =>
        string.Concat(hex.Split(' ').Select(u => (char)ushort.Parse(u, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)));
}
