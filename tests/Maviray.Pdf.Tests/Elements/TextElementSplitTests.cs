using System.Text;
using Maviray.Pdf.Elements;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Tests.Drawing;
using Maviray.Pdf.Tests.Fonts;
using Maviray.Pdf.Tests.Layout;
using Maviray.Pdf.Text;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Elements;

// A Text element split across pages and drawn at another width than it was measured at, as Align, a Row's Auto item
// and a Row's Flex item draw it, must draw every line once (P1.11 review). Each page here holds one 12-point line.
public sealed class TextElementSplitTests
{
    private const string Shape = "Ein langer Satz der breit ist\nkurz a\nkurz b\nkurz c";

    private static readonly string[] ShapeLines = ["Ein langer Satz der breit ist", "kurz a", "kurz b", "kurz c"];

    private static readonly FontRegistry Fonts = new FontRegistry()
        .Register("Noto Sans", File.ReadAllBytes(TestFonts.NotoSansPath("Regular")))
        .Register("Noto Sans", File.ReadAllBytes(TestFonts.NotoSansPath("Bold")));

    private static readonly TextStyle Base = new TextStyle().FontSize(10).LineHeight(1.2);

    [Fact]
    public void APlainTextDrawsEachLineOnce() => Assert.Equal(ShapeLines, Lines(Text(Shape), 12));

    [Fact]
    public void ACentredTextDrawsEachLineOnce() => Assert.Equal(ShapeLines, Lines(new Align(HorizontalAlignment.Center, null, Text(Shape)), 12));

    [Fact]
    public void AnAutoItemDrawsEachLineOnce() => Assert.Equal(ShapeLines, Lines(new Row([new RowItem(RowItemKind.Auto, 0, Text(Shape))], 0), 12));

    // The auto item is a wide block, then a narrow one, so the flex item widens from page 1 to page 2.
    [Fact]
    public void AFlexItemThatWidensDrawsEveryWord()
    {
        var blue = new RgbColor(0, 0, 1);
        var auto = new Column([new FixedBlock("Wide", 300, 12, blue), new FixedBlock("Narrow", 10, 12, blue)], 0);
        var row = new Row([new RowItem(RowItemKind.Auto, 0, auto), new RowItem(RowItemKind.Flex, 1, Text(string.Join(" ", Enumerable.Range(1, 40).Select(i => $"w{i}"))))], 0);

        var words = string.Join(" ", Lines(row, 12)).Split(' ', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(Enumerable.Range(1, 40).Select(i => $"w{i}"), words);
    }

    [Fact]
    public void AWideFirstLineKeepsTheContract() => ElementContract.Check(() => Text(Shape), new Size(495, 12), new Size(495, 24.5));

    [Fact]
    public void AParagraphShowingItsPageNumberSplitsCleanly()
    {
        var text = new TextElement(
            [new TextRun(TextRunKind.Text, "Seite ", new TextStyle()), new TextRun(TextRunKind.PageNumber, "", new TextStyle()), new TextRun(TextRunKind.Text, "\nzwei\ndrei\nvier", new TextStyle())],
            Base,
            new TextStyle(),
            TextAlignment.Left,
            Fonts);

        Assert.Equal(["Seite 1", "zwei", "drei", "vier"], Lines(text, 24));
    }

    private static TextElement Text(string text) => new([new TextRun(TextRunKind.Text, text, new TextStyle())], Base, new TextStyle(), TextAlignment.Left, Fonts);

    /// <summary>Every text line drawn, page after page, with each page's content <paramref name="contentHeight"/> high.</summary>
    private static List<string> Lines(Element content, double contentHeight)
    {
        var canvas = new RecordingCanvas();
        LayoutEngine.Generate([new PageSet(595, contentHeight + 100, new Margins(50, 50, 50, 50), null, null, content, null)], canvas);
        return [.. canvas.Pages.SelectMany(page => page.Calls.Where(call => call.Kind == "TextLine")).Select(LineText)];
    }

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
