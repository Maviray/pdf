using System.IO.Compression;
using Maviray.Pdf.Drawing;
using Maviray.Pdf.Elements;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Tests.Drawing;
using Maviray.Pdf.Tests.Layout;
using Maviray.Pdf.Tests.Validation;
using Maviray.Pdf.Tests.Writer;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Elements;

// Every container in one invoice-like tree of fixed blocks over three pages, on PDF pages: the render must show each
// rectangle where the recording canvas saw it, in the colour drawn there last.
public sealed class ContainersPdfTests
{
    private static readonly RgbColor Blue = new(0, 0, 1);
    private static readonly RgbColor Green = new(0, 0.6, 0);
    private static readonly RgbColor Red = new(0.8, 0, 0);
    private static readonly RgbColor Grey = new(0.85, 0.85, 0.85);
    private static readonly RgbColor Black = new(0, 0, 0);

    [Fact]
    public void AComposedTreePassesQpdf()
    {
        var sets = Sets();
        var recorded = new RecordingCanvas();
        var count = LayoutEngine.Generate(sets, recorded);
        var file = PdfDocumentWriterTests.Write(document => LayoutEngine.Generate(sets, new PdfCanvas(document)), CompressionLevel.Optimal);
        var path = GeneratedFiles.Save(file);

        Assert.Equal(3, count);
        var total = Assert.Single(recorded.Pages[2].Calls, call => call.Color == Red);
        Assert.Equal(420, total.X);

        Qpdf.Check(path);
        var pages = Poppler.Render(path);
        Assert.Equal(count, pages.Count);
        for (var i = 0; i < pages.Count; i++)
        {
            var calls = recorded.Pages[i].Calls;
            foreach (var call in calls.Where(call => call.Width >= 3 && call.Height >= 3))
            {
                var (x, y) = (call.X + (call.Width / 2), call.Y + (call.Height / 2));
                var expected = calls.Last(c => x >= c.X && x <= c.X + c.Width && y >= c.Y && y <= c.Y + c.Height).Color!.Value;
                var (r, g, b) = pages[i].GetPixel((int)x, (int)y);
                Assert.True(
                    Close(r, expected.Red) && Close(g, expected.Green) && Close(b, expected.Blue),
                    $"Page {i + 1}: ({x}, {y}) renders as ({r}, {g}, {b}).");
            }

            Assert.Equal(((byte)255, (byte)255, (byte)255), pages[i].GetPixel(10, 10));
        }
    }

    private static PageSet[] Sets()
    {
        var header = new Row(
            [
                new RowItem(RowItemKind.Fixed, 200, new Background(Grey, new Padding(5, 5, 5, 5, Block(190, 40, Blue)))),
                new RowItem(RowItemKind.Flex, 1, new Align(HorizontalAlignment.Right, null, Block(100, 40, Green))),
            ],
            10);
        var items = Enumerable.Range(1, 60).Select(_ => (Element)new Border(
            0,
            0,
            0,
            1,
            Grey,
            new Row(
                [
                    new RowItem(RowItemKind.Fixed, 30, Block(20, 12, Black)),
                    new RowItem(RowItemKind.Flex, 1, Block(200, 12, Blue)),
                    new RowItem(RowItemKind.Auto, 0, Block(60, 12, Green)),
                ],
                5)));
        var totalBox = new Align(HorizontalAlignment.Right, null, new Border(1, 1, 1, 1, Black, new Padding(4, 4, 4, 4, Block(120, 20, Red))));
        var content = new Column([header, new HorizontalLine(1, Black), .. items, new PageBreak(), SizeLimit.MinHeight(30, totalBox)], 4);
        return [new PageSet(595, 842, new Margins(50, 50, 50, 50), null, Block(495, 10, Grey), content, Block(495, 10, Grey))];
    }

    private static FixedBlock Block(double width, double height, RgbColor color) => new("Block", width, height, color);

    private static bool Close(byte channel, double component) => Math.Abs(channel - (component * 255)) <= 3;
}
