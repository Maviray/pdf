using Maviray.Pdf.Elements;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Tests.Drawing;
using Maviray.Pdf.Tests.Layout;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Elements;

// Containers draw each child at its measured size, so a sum that rounds differently when drawn must not lose the last
// child (P1.10 review). Lengths here are fractional, as real ones are.
public sealed class FractionalLayoutTests
{
    private static readonly RgbColor Blue = new(0, 0, 1);
    private static readonly RgbColor Red = new(1, 0, 0);

    [Fact]
    public void ANestedColumnDrawnAtItsMeasuredHeightDrawsEveryItem()
    {
        // 14.4 + (1.5 + 0.5) is 16.4, but 16.4 - (14.4 + 1.5) is a hair under 0.5.
        var inner = new Column([new HorizontalLine(14.4, Blue), new HorizontalLine(0.5, Red)], 1.5);

        var canvas = Generate(new Column([inner, new FixedBlock("After", 100, 20, Blue)], 4));

        Assert.Single(canvas.Pages);
        Assert.Single(canvas.Pages[0].Calls, call => call.Color == Red);
    }

    [Fact]
    public void ARowItemDrawnAtTheRowsHeightDrawsEveryItem()
    {
        var inner = new Column([new HorizontalLine(14.4, Blue), new HorizontalLine(0.5, Red)], 1.5);
        var row = new Row([new RowItem(RowItemKind.Fixed, 100, inner)], 0);

        var canvas = Generate(new Column([row, new FixedBlock("After", 100, 20, Blue)], 4));

        Assert.Single(canvas.Pages);
        Assert.Single(canvas.Pages[0].Calls, call => call.Color == Red);
    }

    [Fact]
    public void FractionalColumnsKeepTheContract()
    {
        ElementContract.Check(
            () => new Column([new HorizontalLine(14.4, Blue), new HorizontalLine(0.5, Red)], 1.5),
            new Size(100.3, 700.7), new Size(50.1, 16.4), new Size(10.9, 15.95));
        ElementContract.Check(
            () => new Padding(0, 0.5, 0, 1.5, new HorizontalLine(14.4, Blue)),
            new Size(100.3, 700.7), new Size(50.1, 16.4));
    }

    // Random fractional thicknesses and spacings, as rules and text heights are; the seed is logged on failure.
    [Fact]
    public void NoFractionalColumnLosesAnItem()
    {
        var seed = Environment.GetEnvironmentVariable("SEED") is { } text ? int.Parse(text, System.Globalization.CultureInfo.InvariantCulture) : Random.Shared.Next();
        var random = new Random(seed);
        for (var trial = 0; trial < 500; trial++)
        {
            var count = random.Next(2, 6);
            var thicknesses = Enumerable.Range(0, count).Select(_ => Math.Round(random.NextDouble() * 20, 2) + 0.05).ToArray();
            var spacing = Math.Round(random.NextDouble() * 3, 2);
            var inner = new Column(thicknesses.Select(t => (Element)new HorizontalLine(t, Red)), spacing);

            var canvas = Generate(new Column([inner, new FixedBlock("After", 100, 20, Blue)], Math.Round(random.NextDouble() * 5, 2)));

            var drawn = canvas.Pages.Sum(page => page.Calls.Count(call => call.Color == Red));
            Assert.True(drawn == count, $"Re-run with SEED={seed}: trial {trial} drew {drawn} of {count} rules {string.Join(", ", thicknesses)} with spacing {spacing}.");
        }
    }

    private static RecordingCanvas Generate(Element content)
    {
        var canvas = new RecordingCanvas();
        LayoutEngine.Generate([new PageSet(595, 842, new Margins(50, 50, 50, 50), null, null, content, null)], canvas);
        return canvas;
    }
}
