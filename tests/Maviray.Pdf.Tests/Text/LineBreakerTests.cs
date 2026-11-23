using Maviray.Pdf.Text;

namespace Maviray.Pdf.Tests.Text;

// Expected values from UAX #14 for Unicode 16.0, as the P1.6 plan transcribes it. Characters beyond ASCII are built
// from their code points, so the test source stays ASCII.
public sealed class LineBreakerTests
{
    private const LineBreakKind None = LineBreakKind.None;
    private const LineBreakKind Allowed = LineBreakKind.Allowed;
    private const LineBreakKind Required = LineBreakKind.Required;

    [Theory]
    [InlineData(0x000B)]
    [InlineData(0x000C)]
    [InlineData(0x2028)]
    [InlineData(0x2029)]
    [InlineData(0x000D)]
    [InlineData(0x000A)]
    [InlineData(0x0085)]
    public void BreakCharactersRequireABreak(int breakCharacter)
    {
        var text = "a" + char.ConvertFromUtf32(breakCharacter) + "b";

        Assert.Equal([None, None, Required, Required], LineBreaker.FindBreaks(text));
    }

    [Fact]
    public void CrLfIsOneBreak()
    {
        Assert.Equal([None, None, None, Required, Required], LineBreaker.FindBreaks("a\r\nb"));
    }

    [Fact]
    public void EndsWithARequiredBreak()
    {
        Assert.Equal([None, Required], LineBreaker.FindBreaks("a"));
        Assert.Equal([None, None, Required], LineBreaker.FindBreaks("a\n"));
        Assert.Equal([None], LineBreaker.FindBreaks(""));
    }

    [Fact]
    public void SpacesHangBeforeTheBreak()
    {
        Assert.Equal([None, None, None, Allowed, None, Required], LineBreaker.FindBreaks("ab cd"));
        Assert.Equal([None, None, None, None, Allowed, None, Required], LineBreaker.FindBreaks("ab  cd"));
    }

    [Fact]
    public void NoBreakInsideASurrogatePair()
    {
        var face = char.ConvertFromUtf32(0x1F600);

        Assert.Equal([None, None, Allowed, None, Required], LineBreaker.FindBreaks(face + face));
    }

    [Fact]
    public void MarksAtTheEdges()
    {
        var acute = char.ConvertFromUtf32(0x0301);

        // LB10: a mark with nothing to attach to is AL, so it doesn't break from the letter after it (LB28).
        Assert.Equal([None, None, Required], LineBreaker.FindBreaks(acute + "a"));

        // LB18: a mark after a space doesn't attach to it, and the break after the space stands.
        Assert.Equal([None, Allowed, None, Required], LineBreaker.FindBreaks(" " + acute + "a"));

        // LB4/LB5: a mark after a line feed starts a new line.
        Assert.Equal([None, Required, Required], LineBreaker.FindBreaks("\n" + acute));
    }

    [Fact]
    public void RefusesNull()
    {
        Assert.Throws<ArgumentNullException>(() => LineBreaker.FindBreaks(null!));
    }

    // Each run would be quadratic if a rule scanned it again at every boundary.
    [Fact(Timeout = 10_000)]
    public async Task LongRunsStayLinear()
    {
        const int Run = 100_000;
        var token = TestContext.Current.CancellationToken;

        // LB14: no break after an opening parenthesis and any number of spaces.
        var spaces = "(" + new string(' ', Run) + "a";
        var spaceBreaks = await Task.Run(() => LineBreaker.FindBreaks(spaces), token);
        Assert.Equal(None, spaceBreaks[Run + 1]);

        // LB30a: regional indicators pair up, so only every second boundary between them may break.
        var flags = string.Concat(Enumerable.Repeat(char.ConvertFromUtf32(0x1F1E6), Run));
        var flagBreaks = await Task.Run(() => LineBreaker.FindBreaks(flags), token);
        Assert.Equal(None, flagBreaks[2]);
        Assert.Equal(Allowed, flagBreaks[4]);
        Assert.Equal(None, flagBreaks[(2 * Run) - 2]);

        // LB25: a number with any run of infix separators stays together.
        var number = "1" + new string('.', Run) + "1";
        var numberBreaks = await Task.Run(() => LineBreaker.FindBreaks(number), token);
        Assert.Equal(None, numberBreaks[Run + 1]);
    }
}
