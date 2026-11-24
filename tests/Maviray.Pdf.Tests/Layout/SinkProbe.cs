using Maviray.Pdf.Drawing;
using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Tests.Layout;

/// <summary>A test element of no size that records, for each draw, whether it was the render pass and had a sink.</summary>
internal sealed class SinkProbe(string name) : Element(name)
{
    public List<(bool Render, bool HasSink)> Draws { get; } = [];

    public override Measurement Measure(Size available, PageContext page) => Measurement.Full(default, this);

    public override bool Draw(Size available, ICanvas canvas, PageContext page)
    {
        Draws.Add((page.IsRenderPass, page.Diagnostics is not null));
        return true;
    }

    public override void Reset()
    {
    }
}
