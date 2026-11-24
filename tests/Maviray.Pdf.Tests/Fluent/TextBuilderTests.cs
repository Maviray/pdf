using System.Text;
using Maviray.Pdf.Elements;
using Maviray.Pdf.Fluent.Nodes;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Tests.Drawing;
using Maviray.Pdf.Tests.Elements;
using Maviray.Pdf.Tests.Fonts;
using Maviray.Pdf.Tests.Layout;

namespace Maviray.Pdf.Tests.Fluent;

// The text calls of L-02: .Text(string) with its span builder, and .Text(t => ...) with spans, page numbers and alignment.
public sealed class TextBuilderTests
{
    private static readonly FontRegistry Fonts = new FontRegistry()
        .Register("Noto Sans", File.ReadAllBytes(TestFonts.NotoSansPath("Regular")))
        .Register("Noto Sans", File.ReadAllBytes(TestFonts.NotoSansPath("Bold")));

    private static readonly Size Space = new(495, 742);

    [Fact]
    public void ASpanIsStyledByItsBuilder()
    {
        var root = new ContainerSlot();
        root.Text("fett").Bold().FontSize(14).FontColor(Color.FromHex("#CC0000"));

        var line = Assert.Single(ElementCanvas.Draw(Build(root), Space).Calls);

        var span = line.Layout!.Source.Source.Spans[0];
        Assert.Equal(("NotoSans-Bold", 14.0), (span.Style.Font.PostScriptName, span.Style.Size));
        Assert.Equal([Color.FromHex("#CC0000").ToRgb()], line.SpanColors);
    }

    [Fact]
    public void PageNumberAndPageCountSpans()
    {
        var footer = new ContainerSlot();
        footer.Text(text =>
        {
            text.Span("Seite ");
            text.PageNumber();
            text.Span(" von ");
            text.PageCount();
        });
        var canvas = new RecordingCanvas();
        var content = new Column([new FixedBlock("A", 10, 10, Color.Black.ToRgb())], 0);

        LayoutEngine.Generate([new PageSet(595, 842, new Margins(50, 50, 50, 50), null, null, content, Build(footer))], canvas);

        Assert.Equal("Seite 1 von 1", LineText(canvas.Pages[0].Calls.Single(call => call.Kind == "TextLine")));
    }

    [Fact]
    public void AlignmentApplies()
    {
        var root = new ContainerSlot();
        root.Text(text => text.AlignCenter().Span("Mitte"));

        var (measurement, calls) = ElementCanvas.Draw(Build(root), Space);

        Assert.Equal((495 - measurement.Size.Width) / 2, calls[0].Layout!.Lines[0].Offset, 9);
    }

    [Fact]
    public void ADefaultTextStyleReachesTheTextInside()
    {
        var root = new ContainerSlot();
        root.DefaultTextStyle(style => style.FontSize(20).LineHeight(1)).Text("gross");

        Assert.Equal(20, Build(root).Measure(Space, new PageContext(1, 1, isRenderPass: true)).Size.Height, 6);
    }

    [Fact]
    public void ASpanWinsOverTheInheritedStyle()
    {
        var root = new ContainerSlot();
        root.DefaultTextStyle(style => style.FontSize(20).LineHeight(1)).Text("klein").FontSize(10);

        Assert.Equal(10, Build(root).Measure(Space, new PageContext(1, 1, isRenderPass: true)).Size.Height, 6);
    }

    [Fact]
    public void AStyleCallAfterBuildingThrows()
    {
        var root = new ContainerSlot();
        var span = root.Text("spaet");
        Build(root);

        Assert.Throws<InvalidOperationException>(() => span.Bold());
    }

    [Fact]
    public void NullTextThrows()
    {
        Assert.Throws<ArgumentNullException>(() => new ContainerSlot().Text((string)null!));
        new ContainerSlot().Text(text => Assert.Throws<ArgumentNullException>(() => text.Span(null!)));
    }

    [Fact]
    public void ASpanBuildersArgumentsAreChecked()
    {
        var span = new ContainerSlot().Text("x");

        Assert.Throws<ArgumentOutOfRangeException>(() => span.FontSize(0));
        Assert.Throws<ArgumentException>(() => span.FontFamily(" "));
    }

    private static Element Build(ContainerSlot root) => root.Build(new BuildContext(new TextStyle(), Fonts));

    private static string LineText(CanvasCall call)
    {
        var text = new StringBuilder();
        foreach (var run in call.Layout!.Lines[call.LineIndex].Runs)
        {
            foreach (var glyph in run.Glyphs)
            {
                text.Append(char.ConvertFromUtf32(glyph.CodePoint));
            }
        }

        return text.ToString();
    }
}
