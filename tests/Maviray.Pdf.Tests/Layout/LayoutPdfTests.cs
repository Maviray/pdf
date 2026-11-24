using System.IO.Compression;
using Maviray.Pdf.Drawing;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Tests.Drawing;
using Maviray.Pdf.Tests.Validation;
using Maviray.Pdf.Tests.Writer;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Layout;

// P1.9 is done when a test tree of fixed blocks pages correctly (spec, Work items). Here it does so on PDF pages: the
// render must put every block where the recording canvas saw it, which checks the PDF canvas's y flip end to end.
public sealed class LayoutPdfTests
{
    private static readonly RgbColor Blue = new(0, 0, 1);
    private static readonly RgbColor Black = new(0, 0, 0);
    private static readonly RgbColor Grey = new(0.9, 0.9, 0.9);

    [Fact]
    public void ATreeOfFixedBlocksPassesQpdf()
    {
        var sets = Sets();
        var recorded = new RecordingCanvas();
        var count = LayoutEngine.Generate(sets, recorded);

        var file = PdfDocumentWriterTests.Write(document => LayoutEngine.Generate(sets, new PdfCanvas(document)), CompressionLevel.Optimal);
        var path = GeneratedFiles.Save(file);

        Qpdf.Check(path);
        var pages = Poppler.Render(path);
        Assert.Equal(count, pages.Count);
        for (var i = 0; i < pages.Count; i++)
        {
            var render = pages[i];
            foreach (var call in recorded.Pages[i].Calls.Skip(1))
            {
                // The canvas's y grows downward like the image's rows, so a block's centre is the same pixel in both.
                var (r, g, b) = render.GetPixel((int)(call.X + (call.Width / 2)), (int)(call.Y + (call.Height / 2)));
                var expected = call.Color!.Value;
                Assert.True(
                    Close(r, expected.Red) && Close(g, expected.Green) && Close(b, expected.Blue),
                    $"Page {i + 1}: the block at ({call.X}, {call.Y}) renders as ({r}, {g}, {b}).");
            }

            var margin = render.GetPixel(20, 20);
            Assert.True(Close(margin.Item1, 0.9) && Close(margin.Item2, 0.9) && Close(margin.Item3, 0.9), $"Page {i + 1}: the margin renders as {margin}.");
        }
    }

    /// <summary>Two page sets: 10 blocks on portrait pages, then 6 on landscape ones, each with a grey background.</summary>
    private static PageSet[] Sets() =>
    [
        new(595, 842, new Margins(50, 50, 50, 50), Grey, Block("Head", 495, 10, Black), Stack(10, 200), Block("Foot", 495, 10, Black)),
        new(842, 595, new Margins(40, 40, 40, 40), Grey, Block("Head", 762, 12, Black), Stack(6, 120), Block("Foot", 762, 12, Black)),
    ];

    private static FixedBlock Block(string name, double width, double height, RgbColor color) => new(name, width, height, color);

    private static TestStack Stack(int count, double width) =>
        new("Stack", Enumerable.Range(1, count).Select(i => Block($"Block {i}", width, 100, Blue)));

    private static bool Close(byte channel, double component) => Math.Abs(channel - (component * 255)) <= 2;
}
