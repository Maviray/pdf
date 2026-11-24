using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Drawing;

public sealed class RecordingCanvasTests
{
    private static readonly RgbColor Red = new(1, 0, 0);

    [Fact]
    public void ItRecordsAbsolutePositions()
    {
        var canvas = new RecordingCanvas();
        canvas.BeginPage(595, 842);
        canvas.PushOffset(10, 20);
        canvas.PushOffset(5, 5);
        canvas.FillRectangle(1, 1, 2, 3, Red);
        canvas.PopOffset();
        canvas.FillRectangle(0, 0, 2, 3, Red);
        canvas.PopOffset();
        canvas.EndPage();

        var page = Assert.Single(canvas.Pages);
        Assert.Equal((595.0, 842.0), (page.Width, page.Height));
        Assert.Equal([new CanvasCall("Rectangle", 16, 26, 2, 3, Red), new CanvasCall("Rectangle", 10, 20, 2, 3, Red)], page.Calls);
    }

    // A layout that pages forever fails its test instead of hanging it.
    [Fact]
    public void ItStopsAfterAThousandPages()
    {
        var canvas = new RecordingCanvas();
        for (var page = 0; page < RecordingCanvas.MaxPages; page++)
        {
            canvas.BeginPage(10, 10);
            canvas.EndPage();
        }

        Assert.Throws<InvalidOperationException>(() => canvas.BeginPage(10, 10));
    }

    [Fact]
    public void ItKeepsThePageAndOffsetRules()
    {
        var canvas = new RecordingCanvas();

        Assert.Throws<InvalidOperationException>(() => canvas.FillRectangle(0, 0, 1, 1, Red));
        canvas.BeginPage(10, 10);
        Assert.Throws<InvalidOperationException>(canvas.PopOffset);
        canvas.PushOffset(1, 1);
        Assert.Throws<InvalidOperationException>(canvas.EndPage);
    }
}
