using Maviray.Pdf.Fluent.Nodes;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Tests.Drawing;
using Maviray.Pdf.Tests.Elements;
using Maviray.Pdf.Tests.Layout;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Fluent;

// The container calls of L-02 on IContainer: each makes its element, checks its arguments at the call, and takes one child.
public sealed class ContainerExtensionsTests
{
    private static readonly RgbColor Blue = new(0, 0, 1);
    private static readonly Size Space = new(495, 742);

    [Fact]
    public void PaddingInsetsItsChild()
    {
        var root = new ContainerSlot();
        Put(root.PaddingLeft(3).PaddingTop(4).PaddingHorizontal(1).PaddingVertical(2).Padding(5), Block(10, 10));

        var call = Single(root);

        Assert.Equal((3 + 1 + 5.0, 4 + 2 + 5.0), (call.X, call.Y));
    }

    [Fact]
    public void AUnitOverloadEqualsThePointOne()
    {
        var inches = new ContainerSlot();
        var points = new ContainerSlot();
        Put(inches.Padding(1, Unit.Inch).Width(25.4, Unit.Millimetre), new SpaceProbe("Probe", 1, 1, Blue));
        Put(points.Padding(72).Width(72), new SpaceProbe("Probe", 1, 1, Blue));

        Assert.Equal(Single(points), Single(inches));
    }

    [Fact]
    public void ChainedAlignmentsCombine()
    {
        var root = new ContainerSlot();
        Put(root.AlignRight().AlignBottom(), Block(50, 20));

        var (_, calls) = ElementCanvas.Draw(Build(root), new Size(200, 100));

        Assert.Equal((150.0, 80.0), (calls[0].X, calls[0].Y));
    }

    [Fact]
    public void SizeLimitsBackgroundsBordersAndLinesMakeTheirElements()
    {
        var root = new ContainerSlot();
        var red = Color.FromRgb(255, 0, 0);
        root.Column(column =>
        {
            Put(column.Item().Width(100).Height(20).Background(red), Block(10, 10));
            Put(column.Item().BorderLeft(2, Color.Black, stretch: false), Block(10, 10));
            column.Item().HorizontalLine(1, Color.Black);
        });

        var calls = ElementCanvas.Draw(Build(root), Space).Calls;

        Assert.Equal(new CanvasCall("Rectangle", 0, 0, 100, 20, red.ToRgb()), calls[0]);
        Assert.Equal(new CanvasCall("Rectangle", 0, 20, 2, 10, Color.Black.ToRgb()), calls[^2]);
        Assert.Equal(new CanvasCall("Rectangle", 0, 30, 495, 1, Color.Black.ToRgb()), calls[^1]);
    }

    [Fact]
    public void ColumnAndRowBuildersPlaceTheirItems()
    {
        var root = new ContainerSlot();
        root.Row(row =>
        {
            row.Spacing(5);
            Put(row.Fixed(100), new SpaceProbe("A", 10, 10, Blue));
            Put(row.Auto(), new SpaceProbe("B", 60, 10, Blue));
            Put(row.Flex(1), new SpaceProbe("C", 10, 10, Blue));
            Put(row.Flex(3), new SpaceProbe("D", 10, 10, Blue));
        });

        var calls = ElementCanvas.Draw(Build(root), Space).Calls;

        Assert.Equal([(0.0, 100.0), (105, 60), (170, 80), (255, 240)], calls.Select(call => (call.X, call.Width)));
    }

    [Fact]
    public void AColumnSpacesItsItems()
    {
        var root = new ContainerSlot();
        root.Column(column =>
        {
            column.Spacing(0.5, Unit.Centimetre);
            Put(column.Item(), Block(10, 10));
            Put(column.Item(), Block(10, 10));
        });

        var calls = ElementCanvas.Draw(Build(root), Space).Calls;

        Assert.Equal(10 + (72 / 2.54 * 0.5), calls[1].Y, 9);
    }

    [Fact]
    public void APageBreakMakesItsElement()
    {
        var root = new ContainerSlot();
        root.PageBreak();

        Assert.Equal(Fit.Partial, Build(root).Measure(Space, new PageContext(1, 1, isRenderPass: true)).Fit);
    }

    [Fact]
    public void ASecondChildThrows()
    {
        var root = new ContainerSlot();
        root.PageBreak();

        Assert.Throws<InvalidOperationException>(() => root.PageBreak());
        Assert.Throws<InvalidOperationException>(() => root.Padding(1));
    }

    [Fact]
    public void AForeignContainerThrows() => Assert.Throws<ArgumentException>(() => new ForeignContainer().Padding(1));

    [Fact]
    public void ANullContainerThrows() => Assert.Throws<ArgumentNullException>(() => ((IContainer)null!).Padding(1));

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void BadLengthsThrowNamingTheParameter(double length)
    {
        var root = new ContainerSlot();

        Assert.Equal("all", Assert.Throws<ArgumentOutOfRangeException>(() => root.Padding(length)).ParamName);
        Assert.Equal("width", Assert.Throws<ArgumentOutOfRangeException>(() => root.Width(length)).ParamName);
        Assert.Equal("thickness", Assert.Throws<ArgumentOutOfRangeException>(() => root.HorizontalLine(length, Color.Black)).ParamName);
    }

    [Fact]
    public void AFlexWeightMustBePositive()
    {
        var root = new ContainerSlot();

        root.Row(row => Assert.Equal("weight", Assert.Throws<ArgumentOutOfRangeException>(() => row.Flex(0)).ParamName));
    }

    private static FixedBlock Block(double width, double height) => new("Block", width, height, Blue);

    private static void Put(IContainer container, Element element) => ((ContainerSlot)container).Set(new ElementNode(element));

    private static Element Build(ContainerSlot root) => root.Build(new BuildContext(new TextStyle(), new FontRegistry()));

    private static CanvasCall Single(ContainerSlot root) => Assert.Single(ElementCanvas.Draw(Build(root), Space).Calls);

}
