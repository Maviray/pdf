using Maviray.Pdf.Drawing;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Layout;

/// <summary>
/// A test element that shows the page count, 10 points wide, whose height depends on the count it is shown. It records
/// every count it is drawn with, in order, across passes. With <c>showsPageCount</c> false it only records, so the
/// engine runs no layout passes for it.
/// </summary>
internal sealed class PageCountBlock(string name, Func<int, double> heightForCount, bool showsPageCount = true) : Element(name)
{
    private static readonly RgbColor Black = new(0, 0, 0);

    /// <summary>Each draw's page number, the count it was shown, and whether it was the render pass.</summary>
    public List<(int Page, int Count, bool Render)> Shown { get; } = [];

    public override bool ShowsPageCount => showsPageCount;

    public override Measurement Measure(Size available, PageContext page)
    {
        var size = new Size(10, heightForCount(page.PageCount));
        return LayoutTolerance.Fits(size.Width, available.Width) && LayoutTolerance.Fits(size.Height, available.Height) ? Measurement.Full(size, this) : Measurement.None(this, available, size);
    }

    public override bool Draw(Size available, ICanvas canvas, PageContext page)
    {
        Shown.Add((page.PageNumber, page.PageCount, page.IsRenderPass));
        canvas.FillRectangle(0, 0, 10, heightForCount(page.PageCount), Black);
        return true;
    }

    public override void Reset()
    {
    }
}
