using Maviray.Pdf.Fonts;
using Maviray.Pdf.Text;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Drawing;

/// <summary>
/// The canvas of the render pass (spec L-10). It maps the canvas, with its origin at the page's top left and y growing
/// downward, onto the page's <see cref="ContentStreamBuilder"/>, where y becomes the page height minus y. The offsets
/// are kept here and every position is written absolute, so the content stream needs no saved graphics state.
/// </summary>
internal sealed class PdfCanvas : ICanvas
{
    private readonly PdfDocumentWriter _writer;
    private readonly Dictionary<TrueTypeFont, EmbeddedFont> _fonts = [];
    private readonly Stack<(double X, double Y)> _offsets = new();
    private PdfPageWriter? _page;
    private double _x;
    private double _y;

    public PdfCanvas(PdfDocumentWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        _writer = writer;
    }

    /// <exception cref="InvalidOperationException">A page is already open.</exception>
    public void BeginPage(double width, double height)
    {
        if (_page is not null)
        {
            throw new InvalidOperationException("A page is already open.");
        }

        _page = _writer.BeginPage(width, height);
    }

    /// <summary>Ends the page, which the writer then writes out (P0 D-06).</summary>
    /// <exception cref="InvalidOperationException">No page is open, or an offset is still pushed.</exception>
    public void EndPage()
    {
        var page = Page();
        if (_offsets.Count > 0)
        {
            throw new InvalidOperationException($"{_offsets.Count} offsets are still pushed at the end of the page.");
        }

        _writer.EndPage(page);
        _page = null;
    }

    public void PushOffset(double x, double y)
    {
        _ = Page();
        _offsets.Push((_x, _y));
        _x += x;
        _y += y;
    }

    /// <exception cref="InvalidOperationException">No offset is pushed.</exception>
    public void PopOffset()
    {
        if (_offsets.Count == 0)
        {
            throw new InvalidOperationException("No offset is pushed.");
        }

        (_x, _y) = _offsets.Pop();
    }

    public void FillRectangle(double x, double y, double width, double height, RgbColor color)
    {
        var page = Page();
        page.Content.SetFillColor(color).Rectangle(_x + x, page.Height - (_y + y) - height, width, height).Fill();
    }

    /// <remarks>
    /// Each glyph run is one text object: its colour, its font and size, one text matrix at the run's start on the
    /// baseline, and its glyphs with their extra advances (T-13). Each glyph's CID comes from its embedded font (F-07),
    /// and each font is embedded on first use.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="spanColors"/> doesn't hold a colour for every span.</exception>
    public void DrawTextLine(TextLayout layout, int lineIndex, double x, double y, IReadOnlyList<RgbColor> spanColors)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(spanColors);
        var spans = layout.Source.Source.Spans;
        if (spanColors.Count != spans.Count)
        {
            throw new ArgumentException($"The paragraph has {spans.Count} spans, and {spanColors.Count} colours were given.", nameof(spanColors));
        }

        var page = Page();
        var line = layout.Lines[lineIndex];
        foreach (var run in line.Runs)
        {
            if (run.Glyphs.Count == 0)
            {
                continue;
            }

            var style = spans[run.SpanIndex].Style;
            var font = Embed(style.Font);
            var cids = new ushort[run.Glyphs.Count];
            var extras = new double[run.Glyphs.Count];
            for (var i = 0; i < cids.Length; i++)
            {
                cids[i] = font.Encode(run.Glyphs[i].GlyphId, run.Glyphs[i].CodePoint);
                extras[i] = run.Glyphs[i].ExtraAdvance;
            }

            page.Content.SetFillColor(spanColors[run.SpanIndex])
                .BeginText()
                .SetFont(font, style.Size)
                .SetTextMatrix(1, 0, 0, 1, _x + x + run.X, page.Height - (_y + y + line.Baseline))
                .ShowGlyphs(cids, extras)
                .EndText();
        }

        foreach (var decoration in line.Decorations)
        {
            FillRectangle(x + decoration.X, y + decoration.Top, decoration.Width, decoration.Thickness, spanColors[decoration.SpanIndex]);
        }
    }

    private EmbeddedFont Embed(TrueTypeFont font)
    {
        if (!_fonts.TryGetValue(font, out var embedded))
        {
            embedded = _writer.EmbedFont(font);
            _fonts.Add(font, embedded);
        }

        return embedded;
    }

    private PdfPageWriter Page() => _page ?? throw new InvalidOperationException("No page is open.");
}
