using Maviray.Pdf.Text;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Drawing;

/// <summary>
/// The canvas of the layout passes (spec L-04, L-10): it draws nothing, encodes no glyphs and keeps no state, so its
/// rules for pages and offsets don't apply.
/// </summary>
internal sealed class NullCanvas : ICanvas
{
    private NullCanvas()
    {
    }

    public static NullCanvas Instance { get; } = new();

    public void BeginPage(double width, double height)
    {
    }

    public void EndPage()
    {
    }

    public void PushOffset(double x, double y)
    {
    }

    public void PopOffset()
    {
    }

    public void FillRectangle(double x, double y, double width, double height, RgbColor color)
    {
    }

    public void DrawTextLine(TextLayout layout, int lineIndex, double x, double y, IReadOnlyList<RgbColor> spanColors)
    {
    }
}
