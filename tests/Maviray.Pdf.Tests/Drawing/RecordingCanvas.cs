using Maviray.Pdf.Drawing;
using Maviray.Pdf.Text;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Drawing;

/// <summary>
/// A canvas for tests (spec L-10): it records every call at its absolute position, so layout tests assert positions
/// without a PDF. It keeps the PDF canvas's rules for pages and offsets, so a layout that breaks them fails here too.
/// </summary>
internal sealed class RecordingCanvas : ICanvas
{
    private readonly Stack<(double X, double Y)> _offsets = new();
    private RecordedPage? _page;
    private double _x;
    private double _y;

    public List<RecordedPage> Pages { get; } = [];

    public void BeginPage(double width, double height)
    {
        if (_page is not null)
        {
            throw new InvalidOperationException("A page is already open.");
        }

        _page = new RecordedPage(width, height, []);
        Pages.Add(_page);
    }

    public void EndPage()
    {
        _ = Page();
        if (_offsets.Count > 0)
        {
            throw new InvalidOperationException($"{_offsets.Count} offsets are still pushed at the end of the page.");
        }

        _page = null;
    }

    public void PushOffset(double x, double y)
    {
        _ = Page();
        _offsets.Push((_x, _y));
        _x += x;
        _y += y;
    }

    public void PopOffset()
    {
        if (_offsets.Count == 0)
        {
            throw new InvalidOperationException("No offset is pushed.");
        }

        (_x, _y) = _offsets.Pop();
    }

    public void FillRectangle(double x, double y, double width, double height, RgbColor color) =>
        Page().Calls.Add(new CanvasCall("Rectangle", _x + x, _y + y, width, height, color));

    public void DrawTextLine(TextLayout layout, int lineIndex, double x, double y, IReadOnlyList<RgbColor> spanColors) =>
        Page().Calls.Add(new CanvasCall("TextLine", _x + x, _y + y, Layout: layout, LineIndex: lineIndex, SpanColors: spanColors));

    private RecordedPage Page() => _page ?? throw new InvalidOperationException("No page is open.");
}
