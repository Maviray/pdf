using Maviray.Pdf.Layout;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Layout;

// The engine's tests rest on the test elements, so each keeps L-03's contract, and the contract check catches one that
// doesn't.
public sealed class TestElementContractTests
{
    private static readonly RgbColor Black = new(0, 0, 0);

    private static readonly Size[] Sizes = [new(500, 700), new(100, 250), new(50, 5), new(0, 0)];

    [Fact]
    public void FixedBlockKeepsTheContract() => ElementContract.Check(() => new FixedBlock("Block", 100, 50, Black), Sizes);

    [Fact]
    public void SplittingBlockKeepsTheContract() => ElementContract.Check(() => new SplittingBlock("Lines", 100, 12, 40, Black), Sizes);

    [Fact]
    public void TestStackKeepsTheContract() => ElementContract.Check(
        () => new TestStack("Stack", [new FixedBlock("A", 80, 100, Black), new SplittingBlock("B", 100, 12, 30, Black), new FixedBlock("C", 90, 60, Black)]),
        Sizes);

    [Fact]
    public void PageCountBlockKeepsTheContract() => ElementContract.Check(() => new PageCountBlock("Count", count => 10 * count), Sizes);

    [Fact]
    public void TheCheckCatchesAnElementThatSplitsWithoutAdvancing()
    {
        var failure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => ElementContract.Check(() => new StallingBlock("Stalling"), new Size(100, 100)));

        Assert.Contains("was Partial, and its draw didn't advance", failure.Message, StringComparison.Ordinal);
    }

    // Containers draw each child at its measured size (P1.10 plan, ruling 1), so it must measure the same there.
    [Fact]
    public void TheCheckCatchesASizeThatMeasuresDifferently()
    {
        var failure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => ElementContract.Check(() => new PickyBlock("Picky", 10, 10), new Size(100, 100)));

        Assert.Contains("at its measured size", failure.Message, StringComparison.Ordinal);
    }

    // A draw after Full finishes the element, which counts as progress, so a container can count on it.
    [Fact]
    public void TheCheckCatchesAFullDrawThatDoesntAdvance()
    {
        var failure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(
            () => ElementContract.Check(() => new FixedBlock("Lazy", 10, 10, Black, advances: false), new Size(100, 100)));

        Assert.Contains("was Full, and its draw didn't advance", failure.Message, StringComparison.Ordinal);
    }
}
