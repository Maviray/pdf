using Maviray.Pdf.Layout;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Layout;

public sealed class ElementTests
{
    private static readonly RgbColor Black = new(0, 0, 0);

    [Fact]
    public void APathRunsFromTheTopElementDown()
    {
        var block = new FixedBlock("Block", 10, 10, Black);
        var outer = new TestStack("Outer", [new TestStack("Stack", [block])]);

        Assert.Equal("Outer > Stack > Block", block.Path);
        Assert.Equal("Outer", outer.Path);
        Assert.Null(outer.Parent);
    }

    [Fact]
    public void AdoptingAChildTwiceThrows()
    {
        var block = new FixedBlock("Block", 10, 10, Black);
        _ = new TestStack("First", [block]);

        Assert.Throws<InvalidOperationException>(() => new TestStack("Second", [block]));
    }

    [Fact]
    public void AnElementDoesntShowThePageCountUnlessAChildDoes()
    {
        Assert.False(new TestStack("Stack", [new FixedBlock("Block", 10, 10, Black)]).ShowsPageCount);
        Assert.True(new TestStack("Stack", [new PageCountBlock("Count", _ => 10)]).ShowsPageCount);
    }
}
