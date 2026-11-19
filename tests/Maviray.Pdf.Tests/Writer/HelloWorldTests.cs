using System.IO.Compression;
using Maviray.Pdf.Tests.Validation;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

/// <summary>
/// The P0 gate file (spec D-13). CI uploads it in the <c>generated-pdfs</c> artifact as
/// <c>Maviray.Pdf.Tests/bin/Release/net10.0/generated/HelloWorldTests/HelloWorldPassesEveryValidator.pdf</c>, and
/// <c>docs/qa/checklists/viewer-check.md</c> describes how to check it by hand. Keep this test's name, the layout and the
/// checklist in step.
/// </summary>
public sealed class HelloWorldTests
{
    private const string Greeting = "Hello, world";
    private const string Latin1Line = "Grüße aus Köln – 25 €"; // Grüße aus Köln – 25 €

    [Fact]
    public void HelloWorldPassesEveryValidator()
    {
        var path = GeneratedFiles.Save(PdfDocumentWriterTests.Write(
            document =>
            {
                var page = document.BeginPage(595.2756, 841.8898);
                page.Content
                    .BeginText()
                    .SetFont(StandardFont.Helvetica, 24).SetTextMatrix(1, 0, 0, 1, 72, 750).ShowText(Greeting)
                    .SetFont(StandardFont.Helvetica, 14).SetTextMatrix(1, 0, 0, 1, 72, 720).ShowText(Latin1Line)
                    .EndText()
                    .SetFillColor(new RgbColor(0, 0, 1)).Rectangle(72, 560, 200, 100).Fill()
                    .SetStrokeColor(new RgbColor(1, 0, 0)).SetLineWidth(4).Rectangle(300, 560, 200, 100).Stroke();
                document.EndPage(page);
            },
            CompressionLevel.Optimal));

        Qpdf.Check(path);

        var render = Assert.Single(Poppler.Render(path));
        Assert.Equal(((byte)0, (byte)0, (byte)255), PixelAt(render, 172, 610)); // inside the blue rectangle
        Assert.Equal(((byte)255, (byte)0, (byte)0), PixelAt(render, 300, 610)); // on the red rectangle's left edge
        Assert.Equal(((byte)255, (byte)255, (byte)255), PixelAt(render, 400, 610)); // inside the red outline
        Assert.Equal(((byte)255, (byte)255, (byte)255), PixelAt(render, 50, 50));

        // Blind spot: empty lines and line-end whitespace are dropped, so only the lines' text and order are compared.
        var lines = Poppler.ExtractText(path)
            .Split('\n')
            .Select(line => line.TrimEnd())
            .Where(line => line.Length > 0)
            .ToArray();
        Assert.Equal([Greeting, Latin1Line], lines);
    }

    // PDF puts the origin at the bottom left and the render at the top left; at 72 dpi one point is one pixel.
    private static (byte, byte, byte) PixelAt(PpmImage render, int x, int y) => render.GetPixel(x, render.Height - y);
}
