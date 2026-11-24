using System.Globalization;
using Maviray.Pdf.Drawing;
using Maviray.Pdf.Fonts;
using Maviray.Pdf.Layout;
using Maviray.Pdf.Text;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Elements;

/// <summary>
/// A paragraph of styled spans (spec L-07), named <c>Text</c> in paths.
/// <list type="bullet">
/// <item><b>Preparing.</b> Each span's style is resolved (L-08) and its font matched (L-09), and the paragraph is
/// prepared once, or again whenever a page number or page count it shows changes.</item>
/// <item><b>Filling.</b> What is left of the paragraph, from its cursor, at the available width, keeping the last
/// finite fill and the fill at an infinite width, which a Row measures an Auto item at again and again.</item>
/// <item><b>Measuring.</b> The lines from its cursor that fit the height: all of them is <see cref="Fit.Full"/>, some
/// <see cref="Fit.Partial"/>, none <see cref="Fit.None"/>. Its width is the widest of them, its height their sum. A line
/// among them that is wider than the space holds one cluster that can never fit, and throws.</item>
/// <item><b>Drawing.</b> Those lines, each at its own top less the first one's, and the cursor moves past them. Its
/// first draw in the render pass reports its missing glyphs and substituted faces (L-14).</item>
/// </list>
/// The cursor is a position in the text, the start of the first line not yet drawn, so each page part wraps at the
/// width it is drawn at: an Align or a Row draws a Text at another width than the content's (P1.11 review, which
/// replaced the plan's line index). It is kept as a span and an offset in it, so it survives a page number that
/// changes length when the paragraph is prepared again.
/// </summary>
internal sealed class TextElement : Element
{
    private readonly TextRun[] _runs;
    private readonly TextStyle _style;
    private readonly TextStyle _inherited;
    private readonly TextAlignment _alignment;
    private readonly FontRegistry _fonts;
    private readonly bool _showsPageNumber;
    private readonly bool _showsPageCount;

    private PreparedText? _prepared;
    private TextLayout? _finite;
    private double _finiteWidth;
    private int _finiteFrom;
    private TextLayout? _infinite;
    private int _infiniteFrom;
    private int _cursorSpan;
    private int _cursorOffset;
    private bool _reported;

    /// <param name="runs">The spans, in order.</param>
    /// <param name="style">The element's own style, under each span's.</param>
    /// <param name="inherited">The style it inherits: the nearest container's default over each outer one, over the page's.</param>
    /// <param name="alignment">How its lines are aligned.</param>
    /// <param name="fonts">The registry its families are matched in.</param>
    public TextElement(IReadOnlyList<TextRun> runs, TextStyle style, TextStyle inherited, TextAlignment alignment, FontRegistry fonts)
        : base("Text")
    {
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(inherited);
        ArgumentNullException.ThrowIfNull(fonts);
        _runs = [.. runs];
        _style = style;
        _inherited = inherited;
        _alignment = alignment;
        _fonts = fonts;
        _showsPageNumber = _runs.Any(run => run.Kind == TextRunKind.PageNumber);
        _showsPageCount = _runs.Any(run => run.Kind == TextRunKind.PageCount);
    }

    public override bool ShowsPageCount => _showsPageCount;

    /// <summary>How many times it has filled its paragraph, for the cache's tests.</summary>
    internal int FillCount { get; private set; }

    public override Measurement Measure(Size available, PageContext page)
    {
        var layout = Layout(available, page);
        var (count, size) = Count(layout, available, page);
        if (count == layout.Lines.Count)
        {
            return Measurement.Full(size, this);
        }

        if (count == 0)
        {
            var first = layout.Lines[0];
            return Measurement.None(this, available, new Size(first.Width, first.Height));
        }

        return Measurement.Partial(size, this, available);
    }

    public override bool Draw(Size available, ICanvas canvas, PageContext page)
    {
        var layout = Layout(available, page);
        if (page.IsRenderPass && !_reported)
        {
            _reported = true;
            Report(page);
        }

        var (count, _) = Count(layout, available, page);
        for (var i = 0; i < count; i++)
        {
            canvas.DrawTextLine(layout, i, 0, layout.Lines[i].Top, _prepared!.Colors);
        }

        if (count > 0)
        {
            var last = layout.Lines[count - 1];
            MoveCursor(last.Start + last.Length);
        }

        return count > 0 || layout.Lines.Count == 0;
    }

    public override void Reset()
    {
        _cursorSpan = 0;
        _cursorOffset = 0;
        _reported = false;
    }

    /// <summary>The lines of the fill from the cursor that fit the height, and their size. A line among them wider than the space throws.</summary>
    private (int Count, Size Size) Count(TextLayout layout, Size available, PageContext page)
    {
        var lines = layout.Lines;
        var width = 0.0;
        var height = 0.0;
        var count = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            if (!LayoutTolerance.Fits(height + lines[i].Height, available.Height))
            {
                break;
            }

            if (!LayoutTolerance.Fits(lines[i].Width, available.Width))
            {
                throw LayoutErrors.Error(
                    page,
                    this,
                    $"a character is {LayoutErrors.Length(lines[i].Width)} wide and the space is {LayoutErrors.Length(available.Width)}.",
                    available);
            }

            width = Math.Max(width, lines[i].Width);
            height += lines[i].Height;
            count++;
        }

        return (count, new Size(width, height));
    }

    /// <summary>What is left of the paragraph, from the cursor, filled at the available width and prepared for the page's numbers.</summary>
    private TextLayout Layout(Size available, PageContext page)
    {
        var prepared = Prepare(available, page);
        var from = From(prepared.Paragraph);

        // The engine fills at a positive width; at zero, a paragraph of visible text holds a character too wide anyway.
        var width = available.Width > 0 ? available.Width : LayoutTolerance.Points;
        if (double.IsPositiveInfinity(width))
        {
            if (_infinite is null || _infiniteFrom != from)
            {
                _infinite = Fill(prepared, width, from);
                _infiniteFrom = from;
            }

            return _infinite;
        }

        if (_finite is null || _finiteWidth != width || _finiteFrom != from)
        {
            _finite = Fill(prepared, width, from);
            _finiteWidth = width;
            _finiteFrom = from;
        }

        return _finite;
    }

    /// <summary>The cursor as a position in the prepared text: its span's start plus its offset, at a cluster's start.</summary>
    private int From(PreparedParagraph paragraph)
    {
        var starts = paragraph.SpanStarts;
        if (starts.Count < 2)
        {
            return 0;
        }

        var span = Math.Min(_cursorSpan, starts.Count - 2);
        var from = Math.Min(starts[span] + _cursorOffset, starts[span + 1]);
        while (from < paragraph.Text.Length && !paragraph.IsClusterStart(from))
        {
            from++;
        }

        return from;
    }

    /// <summary>Moves the cursor to <paramref name="position"/> in the prepared text, kept as a span and an offset in it.</summary>
    private void MoveCursor(int position)
    {
        var starts = _prepared!.Paragraph.SpanStarts;
        var span = 0;
        while (span + 1 < starts.Count - 1 && starts[span + 1] <= position)
        {
            span++;
        }

        _cursorSpan = span;
        _cursorOffset = position - starts[span];
    }

    private TextLayout Fill(PreparedText prepared, double width, int from)
    {
        FillCount++;
        return prepared.Paragraph.Fill(width, from);
    }

    private PreparedText Prepare(Size available, PageContext page)
    {
        var number = _showsPageNumber ? page.PageNumber : 0;
        var total = _showsPageCount ? page.PageCount : 0;
        if (_prepared is { } prepared && prepared.Number == number && prepared.Total == total)
        {
            return prepared;
        }

        var spans = new List<TextSpan>(_runs.Length);
        var colors = new RgbColor[_runs.Length];
        var substitutions = new List<(ResolvedTextStyle Style, FontFace Face)>();
        for (var i = 0; i < _runs.Length; i++)
        {
            var run = _runs[i];
            var style = ResolvedTextStyle.Resolve(run.Style, _style, _inherited);
            var match = _fonts.Match(style.Family, style.Weight, style.Italic)
                ?? throw LayoutErrors.Error(page, this, UnknownFamily(style.Family), available);
            if (match.Substituted)
            {
                substitutions.Add((style, match.Face));
            }

            var text = run.Kind switch
            {
                TextRunKind.PageNumber => page.PageNumber.ToString(CultureInfo.InvariantCulture),
                TextRunKind.PageCount => page.PageCount.ToString(CultureInfo.InvariantCulture),
                _ => run.Text,
            };
            var spanStyle = new SpanStyle(match.Face.Font, style.Size, style.LineHeight, style.LetterSpacing * style.Size, style.Underline, style.Strikethrough);
            spans.Add(new TextSpan(text, spanStyle));
            colors[i] = style.Color.ToRgb();
        }

        _prepared = new PreparedText(PreparedParagraph.Prepare(new Paragraph(spans, _alignment)), colors, substitutions, number, total);
        _finite = null;
        _infinite = null;
        return _prepared;
    }

    private string UnknownFamily(string family)
    {
        var families = _fonts.Families;
        var message = families.Count == 0
            ? $"the font family '{family}' isn't registered, and no family is registered."
            : $"the font family '{family}' isn't registered. The registered families are {string.Join(", ", families)}.";
        return string.Equals(family, ResolvedTextStyle.BuiltIn.Family, StringComparison.OrdinalIgnoreCase)
            ? message + " Reference Maviray.Pdf.Fonts.Default and call RegisterNotoSans(), or register a font and make it the default style's family."
            : message;
    }

    private void Report(PageContext page)
    {
        if (page.Diagnostics is not { } sink || _prepared is not { } prepared)
        {
            return;
        }

        var path = $"Page {page.PageNumber} > {Path}";
        foreach (var missing in prepared.Paragraph.Missing)
        {
            var font = prepared.Paragraph.Source.Spans[missing.SpanIndex].Style.Font;
            sink.MissingGlyph(font, missing.CodePoint, path, page.PageNumber, $"{FontName(font)} has no glyph for U+{missing.CodePoint:X4}, which draws as .notdef.");
        }

        foreach (var (style, face) in prepared.Substitutions)
        {
            sink.Substituted(
                style.Family,
                style.Weight,
                style.Italic,
                path,
                page.PageNumber,
                $"No {Slant(style.Italic)} face of weight {style.Weight} in '{style.Family}'; {face.Family} {face.Weight} {Slant(face.Italic)} is used instead.");
        }
    }

    private static string FontName(TrueTypeFont font) => font.PostScriptName ?? font.FamilyName ?? "The font";

    private static string Slant(bool italic) => italic ? "italic" : "upright";
}
