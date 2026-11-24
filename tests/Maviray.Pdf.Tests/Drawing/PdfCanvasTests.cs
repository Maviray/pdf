using System.Text;
using System.Text.RegularExpressions;
using Maviray.Pdf.Drawing;
using Maviray.Pdf.Tests.Text;
using Maviray.Pdf.Tests.Writer;
using Maviray.Pdf.Text;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Drawing;

// The PDF canvas on uncompressed files, so the content stream can be read as text (spec L-10).
public sealed class PdfCanvasTests
{
    private const double PageWidth = 595;
    private const double PageHeight = 842;

    private static readonly RgbColor Red = new(1, 0, 0);
    private static readonly RgbColor Blue = new(0, 0, 1);

    [Fact]
    public void ARectangleAtTheTopLeftLandsAtThePagesTopLeft()
    {
        var file = Draw(canvas => canvas.FillRectangle(0, 0, 100, 50, Red));

        Assert.Contains("1 0 0 rg\n0 792 100 50 re\nf\n", file, StringComparison.Ordinal);
    }

    [Fact]
    public void OffsetsAddUpAndPop()
    {
        var file = Draw(canvas =>
        {
            canvas.PushOffset(10, 20);
            canvas.PushOffset(5, 5);
            canvas.FillRectangle(1, 1, 2, 3, Red);
            canvas.PopOffset();
            canvas.FillRectangle(0, 0, 2, 3, Red);
            canvas.PopOffset();
        });

        Assert.Contains("16 813 2 3 re\nf\n10 819 2 3 re\nf\n", file, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFillColourIsWrittenOnceWhileItDoesntChange()
    {
        var file = Draw(canvas =>
        {
            canvas.FillRectangle(0, 0, 1, 1, Red);
            canvas.FillRectangle(2, 0, 1, 1, Red);
            canvas.FillRectangle(4, 0, 1, 1, Blue);
        });

        Assert.Equal(1, Count(file, "1 0 0 rg\n"));
        Assert.Equal(1, Count(file, "0 0 1 rg\n"));
    }

    [Fact]
    public void PoppingWithoutAPushThrows() => Draw(canvas => Assert.Throws<InvalidOperationException>(canvas.PopOffset));

    [Fact]
    public void EndingAPageWithAnOffsetThrows()
    {
        _ = PdfDocumentWriterTests.Write(document =>
        {
            var canvas = new PdfCanvas(document);
            canvas.BeginPage(PageWidth, PageHeight);
            canvas.PushOffset(1, 1);
            Assert.Throws<InvalidOperationException>(canvas.EndPage);
            canvas.PopOffset();
            canvas.EndPage();
        });
    }

    [Fact]
    public void ATextLineIsDrawnAtItsBaselineWithItsExtraAdvances()
    {
        var layout = Layout(new TextSpan("Ab", new TextStyle(LayoutTestFont.Font, 10, letterSpacing: 1)));
        var line = layout.Lines[0];

        var file = Draw(canvas => canvas.DrawTextLine(layout, 0, 72, 100, [Blue]));

        var tm = $"1 0 0 1 {Number(72 + line.Runs[0].X)} {Number(PageHeight - (100 + line.Baseline))} Tm\n";
        Assert.Matches(new Regex("0 0 1 rg\nBT\n/\\S+ 10 Tf\n" + Regex.Escape(tm) + @"\[<0001> -100 <0002>\] TJ" + "\nET\n"), file);
    }

    [Fact]
    public void ATextLineFollowsTheOffset()
    {
        var layout = Layout(new TextSpan("Ab", new TextStyle(LayoutTestFont.Font, 10)));
        var line = layout.Lines[0];

        var file = Draw(canvas =>
        {
            canvas.PushOffset(10, 20);
            canvas.DrawTextLine(layout, 0, 72, 100, [Blue]);
            canvas.PopOffset();
        });

        Assert.Contains($"1 0 0 1 {Number(82 + line.Runs[0].X)} {Number(PageHeight - (120 + line.Baseline))} Tm\n", file, StringComparison.Ordinal);
    }

    [Fact]
    public void ADecorationIsAFilledRectangleInItsSpansColour()
    {
        var layout = Layout(
            new TextSpan("Ab ", new TextStyle(LayoutTestFont.Font, 10)),
            new TextSpan("cd", new TextStyle(LayoutTestFont.Font, 10, underline: true)));
        var underline = Assert.Single(layout.Lines[0].Decorations);

        var file = Draw(canvas => canvas.DrawTextLine(layout, 0, 72, 100, [Blue, Red]));

        // The red run before it already set the colour, so the rectangle follows it with no colour of its own.
        var rectangle = $"{Number(72 + underline.X)} {Number(PageHeight - (100 + underline.Top + underline.Thickness))} " +
            $"{Number(underline.Width)} {Number(underline.Thickness)} re\nf\n";
        var at = file.IndexOf(rectangle, StringComparison.Ordinal);
        Assert.True(at >= 0, $"No rectangle {rectangle.TrimEnd()} in the content.");
        var colour = file.LastIndexOf(" rg\n", at, StringComparison.Ordinal);
        Assert.Equal("1 0 0", file[(file.LastIndexOf('\n', colour) + 1)..colour]);
    }

    [Fact]
    public void EachFontIsEmbeddedOnce()
    {
        var layout = Layout(new TextSpan("Ab", new TextStyle(LayoutTestFont.Font, 10)));

        var file = Latin1(PdfDocumentWriterTests.Write(document =>
        {
            var canvas = new PdfCanvas(document);
            for (var page = 0; page < 2; page++)
            {
                canvas.BeginPage(PageWidth, PageHeight);
                canvas.DrawTextLine(layout, 0, 72, 100, [Blue]);
                canvas.DrawTextLine(layout, 0, 72, 200, [Blue]);
                canvas.EndPage();
            }
        }));

        Assert.Equal(1, Count(file, "/FontFile2"));
    }

    [Fact]
    public void ALineNeedsAColourForEverySpan()
    {
        var layout = Layout(new TextSpan("Ab", new TextStyle(LayoutTestFont.Font, 10)));

        Draw(canvas => Assert.Throws<ArgumentException>(() => canvas.DrawTextLine(layout, 0, 0, 0, [])));
    }

    private static TextLayout Layout(params TextSpan[] spans) => PreparedParagraph.Prepare(new Paragraph(spans)).Fill(1000);

    /// <summary>One A4-sized page drawn by <paramref name="draw"/>, as the file's text.</summary>
    private static string Draw(Action<PdfCanvas> draw) => Latin1(PdfDocumentWriterTests.Write(document =>
    {
        var canvas = new PdfCanvas(document);
        canvas.BeginPage(PageWidth, PageHeight);
        draw(canvas);
        canvas.EndPage();
    }));

    private static string Latin1(byte[] file) => Encoding.Latin1.GetString(file);

    private static int Count(string text, string part) => Regex.Count(text, Regex.Escape(part));

    /// <summary>A number as the content stream writes it (P0 D-03).</summary>
    private static string Number(double value)
    {
        Span<byte> buffer = stackalloc byte[32];
        return Encoding.ASCII.GetString(buffer[..PdfNumberFormat.Format(value, buffer)]);
    }
}
