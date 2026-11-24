using Maviray.Pdf.Layout;
using Maviray.Pdf.Tests.Drawing;

namespace Maviray.Pdf.Tests.Elements;

/// <summary>Measures and draws an element once, on a fresh recording page of 595 × 842, for the container tests.</summary>
internal static class ElementCanvas
{
    public static (Measurement Measurement, List<CanvasCall> Calls) Draw(Element element, Size space)
    {
        ArgumentNullException.ThrowIfNull(element);
        var page = new PageContext(1, 1, isRenderPass: true);
        var canvas = new RecordingCanvas();
        canvas.BeginPage(595, 842);
        var measurement = element.Measure(space, page);
        _ = element.Draw(space, canvas, page);
        canvas.EndPage();
        return (measurement, canvas.Pages[0].Calls);
    }
}
