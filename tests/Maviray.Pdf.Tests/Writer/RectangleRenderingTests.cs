using System.IO.Compression;
using Maviray.Pdf.Tests.Validation;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

public sealed class RectangleRenderingTests
{
    private static readonly (byte, byte, byte) White = (255, 255, 255);

    // The layout is asymmetric, so a check that forgot to flip the y axis would sample white where blue is expected:
    // the unflipped point for the blue rectangle's middle, (172, 192), is outside every rectangle.
    [Fact]
    public void FilledAndStrokedRectanglesRenderWhereExpected()
    {
        var path = GeneratedFiles.Save(PdfDocumentWriterTests.Write(
            document =>
            {
                var page = document.BeginPage(595.2756, 841.8898);
                page.Content
                    .SetFillColor(new RgbColor(0, 0, 1)).Rectangle(72, 600, 200, 100).Fill()
                    .SetStrokeColor(new RgbColor(1, 0, 0)).SetLineWidth(4).Rectangle(300, 200, 100, 150).Stroke();
                document.EndPage(page);
            },
            CompressionLevel.Optimal));

        Qpdf.Check(path);
        var render = Assert.Single(Poppler.Render(path));

        Assert.Equal(((byte)0, (byte)0, (byte)255), PixelAt(render, 172, 650)); // middle of the blue rectangle
        Assert.Equal(((byte)255, (byte)0, (byte)0), PixelAt(render, 300, 275)); // left edge of the red one, mid-stroke
        Assert.Equal(White, PixelAt(render, 350, 275)); // inside the red rectangle, which is only stroked
        Assert.Equal(White, PixelAt(render, 50, 50));
        Assert.Equal(White, PixelAt(render, 500, 750));
    }

    // PDF puts the origin at the bottom left and the render at the top left; at 72 dpi one point is one pixel.
    private static (byte, byte, byte) PixelAt(PpmImage render, int x, int y) => render.GetPixel(x, render.Height - y);
}
