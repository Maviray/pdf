using System.Text;
using Maviray.Pdf.Fonts;

namespace Maviray.Pdf.Text;

/// <summary>
/// A paragraph prepared once, independently of any width (spec T-14): the spans cleaned and normalized one by one and
/// joined (T-03), the grapheme clusters and break opportunities (T-05, T-06), and every code point's glyphs (T-07).
/// <see cref="Fill"/> then lays it out at any width (T-08 to T-11). It is immutable, and holds no document state, so
/// it can be filled many times and shared across threads.
/// </summary>
internal sealed class PreparedParagraph
{
    private const int Tab = 0x0009;
    private const int Space = 0x0020;
    private const int SoftHyphen = 0x00AD;
    private const int Hyphen = 0x2010;
    private const int HyphenMinus = 0x002D;

    /// <summary>The tolerance that absorbs floating-point noise in widths computed by the layout (T-09).</summary>
    private const double FitTolerance = 1e-6;

    // Per code point, with a sentinel at the end: UTF-16 start, span, and where its glyphs start.
    private readonly int[] _codePointStarts;
    private readonly int[] _codePointSpans;
    private readonly int[] _glyphStarts;
    private readonly PreparedGlyph[] _glyphs;

    // Per UTF-16 boundary: the code point that starts there (−1 inside a surrogate pair), and cluster starts.
    private readonly int[] _codePointAt;
    private readonly bool[] _clusterStarts;

    // Per cluster, with a sentinel at the end where it applies.
    private readonly int[] _clusterFirst;
    private readonly ClusterKind[] _clusterKinds;
    private readonly double[] _clusterSpacing;
    private readonly LineBreakKind[] _breakBefore;
    private readonly int[] _nextBreak;
    private readonly double[] _prefixWidth;
    private readonly int[] _lastDrawnBefore;
    private readonly int[] _contentEnd;
    private readonly double[] _negativeAfter;

    // Per span.
    private readonly PreparedGlyph?[] _spanHyphens;
    private readonly double[] _spanAbove;
    private readonly double[] _spanBelow;
    private readonly (double Top, double Thickness)[] _spanUnderlines;
    private readonly (double Top, double Thickness)[] _spanStrikethroughs;

    private PreparedParagraph(Paragraph source, string text, int[] spanStarts, MissingGlyph[] missing, Arrays arrays)
    {
        Source = source;
        Text = text;
        SpanStarts = spanStarts;
        Missing = missing;
        _codePointStarts = arrays.CodePointStarts;
        _codePointSpans = arrays.CodePointSpans;
        _glyphStarts = arrays.GlyphStarts;
        _glyphs = arrays.Glyphs;
        _codePointAt = arrays.CodePointAt;
        _clusterStarts = arrays.ClusterStarts;
        _clusterFirst = arrays.ClusterFirst;
        _clusterKinds = arrays.ClusterKinds;
        _clusterSpacing = arrays.ClusterSpacing;
        _breakBefore = arrays.BreakBefore;
        _nextBreak = arrays.NextBreak;
        _prefixWidth = arrays.PrefixWidth;
        _lastDrawnBefore = arrays.LastDrawnBefore;
        _contentEnd = arrays.ContentEnd;
        _negativeAfter = arrays.NegativeAfter;
        _spanHyphens = arrays.SpanHyphens;
        _spanAbove = arrays.SpanAbove;
        _spanBelow = arrays.SpanBelow;
        _spanUnderlines = arrays.SpanUnderlines;
        _spanStrikethroughs = arrays.SpanStrikethroughs;
    }

    /// <summary>What a cluster is to filling.</summary>
    private enum ClusterKind : byte
    {
        Other,

        /// <summary>Starts with a break character, which ends its line and is never drawn.</summary>
        Break,

        /// <summary>Only U+0020 and U+0009, which hang at a line's end.</summary>
        HangingSpace,

        /// <summary>A soft hyphen, which draws a hyphen only where its line ends.</summary>
        SoftHyphen,
    }

    public Paragraph Source { get; }

    /// <summary>The spans' cleaned and normalized texts, joined. Every range of the engine is a UTF-16 range in it.</summary>
    public string Text { get; }

    /// <summary>Where each span starts in <see cref="Text"/>, then <c>Text.Length</c>.</summary>
    public IReadOnlyList<int> SpanStarts { get; }

    /// <summary>Each code point the font of its span can't draw, once per span, in order of first occurrence.</summary>
    public IReadOnlyList<MissingGlyph> Missing { get; }

    private int ClusterCount => _clusterKinds.Length;

    public static PreparedParagraph Prepare(Paragraph paragraph)
    {
        ArgumentNullException.ThrowIfNull(paragraph);

        var spans = paragraph.Spans;
        var builder = new StringBuilder();
        var spanStarts = new int[spans.Count + 1];
        for (var s = 0; s < spans.Count; s++)
        {
            spanStarts[s] = builder.Length;
            builder.Append(TextNormalizer.ToNfc(spans[s].Text));
        }

        spanStarts[spans.Count] = builder.Length;
        var text = builder.ToString();

        var missing = new List<MissingGlyph>();
        var arrays = ResolveCodePoints(text, spans, spanStarts, missing);
        FindClusters(text, spans, arrays);
        MeasureSpans(spans, arrays);
        return new PreparedParagraph(paragraph, text, spanStarts, [.. missing], arrays);
    }

    /// <summary>The glyphs of the code point that starts at UTF-16 index <paramref name="index"/> of the text.</summary>
    public IReadOnlyList<PreparedGlyph> GlyphsAt(int index)
    {
        if ((uint)index >= (uint)Text.Length || _codePointAt[index] < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The index must be where a code point of the text starts.");
        }

        var codePoint = _codePointAt[index];
        return new ArraySegment<PreparedGlyph>(_glyphs, _glyphStarts[codePoint], _glyphStarts[codePoint + 1] - _glyphStarts[codePoint]);
    }

    /// <summary>Whether a grapheme cluster starts at UTF-16 boundary <paramref name="index"/>, 0 to <c>Text.Length</c>.</summary>
    public bool IsClusterStart(int index) => _clusterStarts[index];

    /// <summary>
    /// Lays the paragraph out at <paramref name="width"/> points (T-08 to T-11): greedy lines, each ended where the
    /// next segment wouldn't fit, at a required break, or between clusters when a word is too wide for any line.
    /// </summary>
    /// <param name="width">Positive and finite, or positive infinity to measure the paragraph's natural width.</param>
    public TextLayout Fill(double width)
    {
        if (!(width > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "The width must be positive, or positive infinity.");
        }

        var lines = new List<TextLine>();
        var top = 0.0;
        var widest = 0.0;
        for (var start = 0; start < ClusterCount;)
        {
            var (end, lineWidth, cut) = FitLine(start, width + FitTolerance);
            var line = BuildLine(start, end, lineWidth, width, top, cut);
            lines.Add(line);
            top += line.Height;
            widest = Math.Max(widest, lineWidth);
            start = end;
        }

        return new TextLayout(this, widest, top, lines);
    }

    /// <summary>Whether the code point ends its line and is never drawn (spec T-06).</summary>
    internal static bool IsBreakCharacter(int codePoint) =>
        codePoint is 0x000A or 0x000B or 0x000C or 0x000D or 0x0085 or 0x2028 or 0x2029;

    /// <summary>
    /// The clusters <c>[start, end)</c> of the next line, its width, and whether it is a cut. The line ends after the
    /// last segment, up to the next required break, with which it fits as it would be drawn (T-09): with the hyphen
    /// where it ends at a soft hyphen after drawn text. The scan stops once no later end can fit: the line's plain
    /// width plus the most that later clusters could narrow it is past the limit. Without negative letter spacing that
    /// bound is the plain width itself, so the scan stops at the first segment that doesn't fit.
    /// </summary>
    private (int End, double Width, bool Cut) FitLine(int start, double limit)
    {
        var end = start;
        var width = 0.0;
        for (var k = start; ;)
        {
            var next = _nextBreak[k];
            if (LowestWidthFrom(start, next) > limit)
            {
                break;
            }

            var finished = LineWidth(start, next);
            if (finished <= limit)
            {
                (end, width) = (next, finished);
            }

            if (next == ClusterCount || _breakBefore[next] == LineBreakKind.Required)
            {
                break;
            }

            k = next;
        }

        return end == start ? Cut(start, _nextBreak[start], limit) : (end, width, false);
    }

    /// <summary>
    /// A lower bound on the width of any line of clusters <c>[start, e)</c> with <c>e</c> at or after
    /// <paramref name="end"/>: its plain width now, plus every negative contribution still possible after its last
    /// drawn cluster, that cluster's own letter spacing among them.
    /// </summary>
    private double LowestWidthFrom(int start, int end)
    {
        var plain = PlainWidth(start, end);
        var lastDrawn = _lastDrawnBefore[Math.Max(start, _contentEnd[end])];
        return lastDrawn >= start
            ? plain + Math.Min(0, _clusterSpacing[lastDrawn]) + _negativeAfter[lastDrawn + 1]
            : plain + _negativeAfter[start];
    }

    /// <summary>
    /// When no segment end fits, the first segment is cut between clusters (T-09): the whole segment when it fits
    /// without its hyphen, which a cut doesn't draw, and otherwise the most clusters that fit, at least one, which may
    /// then overflow. The scan stops at the first prefix that doesn't fit, so a cut costs time in proportion to its own
    /// line, and a long word at a tiny width stays linear.
    /// </summary>
    private (int End, double Width, bool Cut) Cut(int start, int segmentEnd, double limit)
    {
        var whole = PlainWidth(start, segmentEnd);
        if (whole <= limit)
        {
            return (segmentEnd, whole, true);
        }

        var end = start + 1;
        var width = PlainWidth(start, end);
        for (var x = start + 2; x < segmentEnd; x++)
        {
            var candidate = PlainWidth(start, x);
            if (candidate > limit)
            {
                break;
            }

            (end, width) = (x, candidate);
        }

        return (end, width, true);
    }

    /// <summary>
    /// The width of a line of clusters <c>[start, end)</c>: the drawn advances, and each drawn cluster's letter
    /// spacing but the last one's, without the trailing spaces and break character. When the line ends right after a
    /// soft hyphen and text follows, the hyphen is drawn, as the line's last cluster.
    /// </summary>
    private double LineWidth(int start, int end)
    {
        if (EndsAtSoftHyphen(start, end) && _spanHyphens[SpanOfCluster(end - 1)] is { } hyphen)
        {
            return _prefixWidth[end] - _prefixWidth[start] + hyphen.Advance;
        }

        return PlainWidth(start, end);
    }

    /// <summary>The width of a line of clusters <c>[start, end)</c> as if no hyphen were drawn at its end.</summary>
    private double PlainWidth(int start, int end)
    {
        var contentEnd = Math.Max(start, _contentEnd[end]);
        var lastDrawn = _lastDrawnBefore[contentEnd];
        var spacing = lastDrawn >= start ? _clusterSpacing[lastDrawn] : 0;
        return _prefixWidth[contentEnd] - _prefixWidth[start] - spacing;
    }

    /// <summary>
    /// Whether a line of clusters <c>[start, end)</c> ends right after a soft hyphen that follows drawn text, with text
    /// after it: the case where the soft hyphen draws a hyphen (T-07).
    /// </summary>
    private bool EndsAtSoftHyphen(int start, int end) =>
        end > start && end < ClusterCount && _clusterKinds[end - 1] == ClusterKind.SoftHyphen && _lastDrawnBefore[end - 1] >= start;

    private int SpanOfCluster(int cluster) => _codePointSpans[_clusterFirst[cluster]];

    private TextLine BuildLine(int start, int end, double width, double available, double top, bool cut)
    {
        var firstCodePoint = _clusterFirst[start];
        var endCodePoint = _clusterFirst[end];
        var lineStart = _codePointStarts[firstCodePoint];
        var lineEnd = _codePointStarts[endCodePoint];

        // T-08: every span with a code point on the line counts, hanging spaces and the break character included.
        // Every line has one, and either extent may be negative when the leading is.
        var above = double.NegativeInfinity;
        var below = double.NegativeInfinity;
        var spanStarts = SpanStarts;
        for (var s = _codePointSpans[firstCodePoint]; s <= _codePointSpans[endCodePoint - 1]; s++)
        {
            if (spanStarts[s] < spanStarts[s + 1])
            {
                above = Math.Max(above, _spanAbove[s]);
                below = Math.Max(below, _spanBelow[s]);
            }
        }

        // T-10: the line's offset, then its glyphs in order, hanging spaces and the break character left out.
        var offset = width > available || double.IsInfinity(available) ? 0
            : Source.Alignment switch
            {
                TextAlignment.Center => (available - width) / 2,
                TextAlignment.Right => available - width,
                _ => 0,
            };
        var (runs, decorations) = Place(start, end, offset, above, cut);
        return new TextLine(lineStart, lineEnd - lineStart, width, offset, top, above + below, above, runs, decorations);
    }

    /// <summary>
    /// The glyphs of clusters <c>[start, end)</c>, from <paramref name="offset"/>, as one run per span segment, and
    /// the decorations (T-10, T-11). A cluster's last glyph carries its letter spacing as the extra advance after it,
    /// except on the line's last drawn cluster; a hyphen drawn at a soft hyphen at the line's end is that last
    /// cluster. A decoration covers each run of consecutive clusters of one decorated span, from the first one's
    /// start to the end of the last one's advance.
    /// </summary>
    private (List<GlyphRun> Runs, List<TextDecoration> Decorations) Place(int start, int end, double offset, double baseline, bool cut)
    {
        var contentEnd = Math.Max(start, _contentEnd[end]);
        var hyphen = !cut && EndsAtSoftHyphen(start, end) ? _spanHyphens[SpanOfCluster(end - 1)] : null;
        var lastDrawn = hyphen is null ? _lastDrawnBefore[contentEnd] : -1;

        var runs = new List<GlyphRun>();
        var decorations = new List<TextDecoration>();
        var glyphs = new List<PositionedGlyph>();
        var runSpan = -1;
        var decorationSpan = -1;
        var decorationStart = 0.0;
        var decorationEnd = 0.0;
        var decorationDrawn = false;
        var x = offset;
        for (var k = start; k < contentEnd; k++)
        {
            var clusterStart = x;
            var clusterEnd = x;
            var lastGlyph = _glyphStarts[_clusterFirst[k + 1]] - 1;
            for (var c = _clusterFirst[k]; c < _clusterFirst[k + 1]; c++)
            {
                for (var g = _glyphStarts[c]; g < _glyphStarts[c + 1]; g++)
                {
                    var extra = g == lastGlyph && k != lastDrawn ? _clusterSpacing[k] : 0;
                    AddGlyph(_codePointSpans[c], new PositionedGlyph(_glyphs[g].GlyphId, _glyphs[g].CodePoint, x, extra));
                    clusterEnd = x + _glyphs[g].Advance;
                    x = clusterEnd + extra;
                }
            }

            Decorate(SpanOfCluster(k), clusterStart, clusterEnd, lastGlyph >= _glyphStarts[_clusterFirst[k]]);
        }

        if (hyphen is { } drawn)
        {
            var span = SpanOfCluster(end - 1);
            AddGlyph(span, new PositionedGlyph(drawn.GlyphId, drawn.CodePoint, x, 0));
            Decorate(span, x, x + drawn.Advance, true);
        }

        if (glyphs.Count > 0)
        {
            runs.Add(new GlyphRun(runSpan, glyphs[0].X, glyphs.ToArray()));
        }

        FlushDecoration();
        return (runs, decorations);

        void AddGlyph(int span, PositionedGlyph glyph)
        {
            if (span != runSpan && glyphs.Count > 0)
            {
                runs.Add(new GlyphRun(runSpan, glyphs[0].X, glyphs.ToArray()));
                glyphs.Clear();
            }

            runSpan = span;
            glyphs.Add(glyph);
        }

        void Decorate(int span, double clusterStart, double clusterEnd, bool clusterDrawn)
        {
            if (span != decorationSpan)
            {
                FlushDecoration();
                (decorationSpan, decorationStart, decorationDrawn) = (span, clusterStart, false);
            }

            if (clusterDrawn)
            {
                decorationEnd = clusterEnd;
                decorationDrawn = true;
            }
        }

        void FlushDecoration()
        {
            if (decorationSpan < 0 || !decorationDrawn)
            {
                return;
            }

            var style = Source.Spans[decorationSpan].Style;
            var width = decorationEnd - decorationStart;
            if (style.Underline)
            {
                var (top, thickness) = _spanUnderlines[decorationSpan];
                decorations.Add(new TextDecoration(DecorationKind.Underline, decorationSpan, decorationStart, width, baseline - top, thickness));
            }

            if (style.Strikethrough)
            {
                var (top, thickness) = _spanStrikethroughs[decorationSpan];
                decorations.Add(new TextDecoration(DecorationKind.Strikethrough, decorationSpan, decorationStart, width, baseline - top, thickness));
            }
        }
    }

    /// <summary>T-03 and T-07: each code point's span and glyphs.</summary>
    private static Arrays ResolveCodePoints(string text, IReadOnlyList<TextSpan> spans, int[] spanStarts, List<MissingGlyph> missing)
    {
        var codePointStarts = new List<int>(text.Length + 1);
        var codePointSpans = new List<int>(text.Length + 1);
        var codePointAt = new int[text.Length + 1];
        var glyphStarts = new List<int>(text.Length + 1);
        var glyphs = new List<PreparedGlyph>(text.Length);
        var seen = new HashSet<MissingGlyph>();
        Span<int> decomposition = stackalloc int[NormalizationLookup.MaxDecompositionLength];

        var span = 0;
        for (var i = 0; i < text.Length;)
        {
            while (i >= spanStarts[span + 1])
            {
                span++;
            }

            var codePoint = char.ConvertToUtf32(text, i);
            codePointAt[i] = codePointStarts.Count;
            codePointStarts.Add(i);
            codePointSpans.Add(span);
            glyphStarts.Add(glyphs.Count);
            if (Resolve(codePoint, spans[span].Style, glyphs, decomposition) && seen.Add(new MissingGlyph(codePoint, span)))
            {
                missing.Add(new MissingGlyph(codePoint, span));
            }

            if (char.IsSurrogatePair(text, i))
            {
                codePointAt[i + 1] = -1;
                i += 2;
            }
            else
            {
                i++;
            }
        }

        codePointAt[text.Length] = codePointStarts.Count;
        codePointStarts.Add(text.Length);
        codePointSpans.Add(spans.Count == 0 ? 0 : spans.Count - 1);
        glyphStarts.Add(glyphs.Count);

        return new Arrays
        {
            CodePointStarts = [.. codePointStarts],
            CodePointSpans = [.. codePointSpans],
            CodePointAt = codePointAt,
            GlyphStarts = [.. glyphStarts],
            Glyphs = [.. glyphs],
        };
    }

    /// <summary>
    /// T-05, T-06 and what filling needs per cluster: its kind, its letter spacing, the break before it, and the
    /// running sums that make each candidate line's width a constant-time lookup.
    /// </summary>
    private static void FindClusters(string text, IReadOnlyList<TextSpan> spans, Arrays arrays)
    {
        arrays.ClusterStarts = GraphemeClusters.FindStarts(text);
        var breaks = BreakOpportunities.Find(text);
        var codePointCount = arrays.CodePointStarts.Length - 1;

        var first = new List<int>(codePointCount + 1);
        for (var c = 0; c < codePointCount; c++)
        {
            if (arrays.ClusterStarts[arrays.CodePointStarts[c]])
            {
                first.Add(c);
            }
        }

        var count = first.Count;
        first.Add(codePointCount);
        var kinds = new ClusterKind[count];
        var spacing = new double[count];
        var breakBefore = new LineBreakKind[count + 1];
        var prefix = new double[count + 1];
        var lastDrawn = new int[count + 1];
        var contentEnd = new int[count + 1];
        lastDrawn[0] = -1;
        for (var k = 0; k < count; k++)
        {
            var advance = 0.0;
            var allSpaces = true;
            for (var c = first[k]; c < first[k + 1]; c++)
            {
                for (var g = arrays.GlyphStarts[c]; g < arrays.GlyphStarts[c + 1]; g++)
                {
                    advance += arrays.Glyphs[g].Advance;
                }

                var codePoint = char.ConvertToUtf32(text, arrays.CodePointStarts[c]);
                allSpaces &= codePoint is Space or Tab;
            }

            var firstCodePoint = char.ConvertToUtf32(text, arrays.CodePointStarts[first[k]]);
            var drawn = arrays.GlyphStarts[first[k]] < arrays.GlyphStarts[first[k + 1]];
            kinds[k] = IsBreakCharacter(firstCodePoint) ? ClusterKind.Break
                : allSpaces ? ClusterKind.HangingSpace
                : firstCodePoint == SoftHyphen && !drawn ? ClusterKind.SoftHyphen
                : ClusterKind.Other;
            spacing[k] = drawn ? spans[arrays.CodePointSpans[first[k]]].Style.LetterSpacing : 0;
            breakBefore[k] = k == 0 ? LineBreakKind.None : breaks[arrays.CodePointStarts[first[k]]];
            prefix[k + 1] = prefix[k] + (drawn ? advance + spacing[k] : 0);
            lastDrawn[k + 1] = drawn ? k : lastDrawn[k];
            contentEnd[k + 1] = kinds[k] is ClusterKind.Break or ClusterKind.HangingSpace ? contentEnd[k] : k + 1;
        }

        // How much the clusters from each one on could narrow a line at most: their negative contributions, and
        // the most negative letter spacing among them, which a line's last drawn cluster gives back.
        var negativeAfter = new double[count + 1];
        var lowestSpacing = 0.0;
        var negativeSum = 0.0;
        for (var k = count - 1; k >= 0; k--)
        {
            var contribution = prefix[k + 1] - prefix[k];
            negativeSum += Math.Min(0, contribution);
            lowestSpacing = Math.Min(lowestSpacing, spacing[k]);
            negativeAfter[k] = negativeSum + lowestSpacing;
        }

        breakBefore[count] = LineBreakKind.Required;
        var nextBreak = new int[count + 1];
        nextBreak[count] = count;
        for (var k = count - 1; k >= 0; k--)
        {
            nextBreak[k] = breakBefore[k + 1] != LineBreakKind.None ? k + 1 : nextBreak[k + 1];
        }

        arrays.ClusterFirst = [.. first];
        arrays.ClusterKinds = kinds;
        arrays.ClusterSpacing = spacing;
        arrays.BreakBefore = breakBefore;
        arrays.NextBreak = nextBreak;
        arrays.PrefixWidth = prefix;
        arrays.LastDrawnBefore = lastDrawn;
        arrays.ContentEnd = contentEnd;
        arrays.NegativeAfter = negativeAfter;
    }

    /// <summary>
    /// Per span: the hyphen a soft hyphen draws (U+2010, else U+002D, else none), the line extent (T-08), and the
    /// underline and strikethrough (T-11).
    /// </summary>
    private static void MeasureSpans(IReadOnlyList<TextSpan> spans, Arrays arrays)
    {
        arrays.SpanHyphens = new PreparedGlyph?[spans.Count];
        arrays.SpanAbove = new double[spans.Count];
        arrays.SpanBelow = new double[spans.Count];
        arrays.SpanUnderlines = new (double, double)[spans.Count];
        arrays.SpanStrikethroughs = new (double, double)[spans.Count];
        for (var s = 0; s < spans.Count; s++)
        {
            var style = spans[s].Style;
            var font = style.Font;
            var hyphen = font.GetGlyphId(Hyphen);
            if (hyphen == 0)
            {
                hyphen = font.GetGlyphId(HyphenMinus);
            }

            arrays.SpanHyphens[s] = hyphen == 0 ? null : Glyph(font, style.Size, hyphen, SoftHyphen);
            (arrays.SpanAbove[s], arrays.SpanBelow[s]) = FontLineMetrics.Of(font).Extent(style.Size, style.LineHeight);

            // T-11: each stroke's top edge above the baseline, and its thickness, in points, with the fallbacks.
            var scale = style.Size / font.UnitsPerEm;
            var underline = font.UnderlineThickness == 0
                ? (-0.1 * style.Size, 0.05 * style.Size)
                : (font.UnderlinePosition * scale, font.UnderlineThickness * scale);
            arrays.SpanUnderlines[s] = underline;
            arrays.SpanStrikethroughs[s] = font.StrikeoutSize is int size && size > 0 && font.StrikeoutPosition is int position
                ? (position * scale, size * scale)
                : (0.3 * style.Size, underline.Item2);
        }
    }

    /// <summary>
    /// Adds the glyphs of one code point by T-07's rules, in order, and returns whether it is missing from the font.
    /// A soft hyphen draws nothing here: filling decides whether its line ends at it.
    /// </summary>
    private static bool Resolve(int codePoint, SpanStyle style, List<PreparedGlyph> glyphs, Span<int> decomposition)
    {
        var font = style.Font;
        if (IsBreakCharacter(codePoint) || codePoint == SoftHyphen)
        {
            return false;
        }

        if (codePoint == Tab)
        {
            AddIfMapped(font, style.Size, Space, Tab, glyphs);
            return false;
        }

        // Invisible characters draw nothing even when the font maps them: Noto Sans maps U+2060 to a wide empty glyph,
        // and U+034F, U+200E and U+200F to glyphs with outlines.
        if (CharacterProperties.IsDefaultIgnorable(codePoint) || CharacterProperties.IsControl(codePoint))
        {
            return false;
        }

        var glyph = font.GetGlyphId(codePoint);
        if (glyph != 0)
        {
            glyphs.Add(Glyph(font, style.Size, glyph, codePoint));
            return false;
        }

        if (CharacterProperties.IsSpaceSeparator(codePoint) && AddIfMapped(font, style.Size, Space, codePoint, glyphs))
        {
            return false;
        }

        var length = NormalizationLookup.Decompose(codePoint, decomposition);
        if (length > 1 || decomposition[0] != codePoint)
        {
            var all = true;
            for (var k = 0; k < length && all; k++)
            {
                all = font.GetGlyphId(decomposition[k]) != 0;
            }

            if (all)
            {
                for (var k = 0; k < length; k++)
                {
                    glyphs.Add(Glyph(font, style.Size, font.GetGlyphId(decomposition[k]), decomposition[k]));
                }

                return false;
            }
        }

        glyphs.Add(Glyph(font, style.Size, 0, codePoint));
        return true;
    }

    /// <summary>Adds the glyph the font maps <paramref name="drawn"/> to, copying out as <paramref name="copiesOut"/>.</summary>
    private static bool AddIfMapped(TrueTypeFont font, double size, int drawn, int copiesOut, List<PreparedGlyph> glyphs)
    {
        var glyph = font.GetGlyphId(drawn);
        if (glyph == 0)
        {
            return false;
        }

        glyphs.Add(Glyph(font, size, glyph, copiesOut));
        return true;
    }

    private static PreparedGlyph Glyph(TrueTypeFont font, double size, ushort glyph, int codePoint) =>
        new(glyph, codePoint, font.GetAdvanceWidth(glyph) * size / font.UnitsPerEm);

    /// <summary>The arrays preparation builds, gathered while they are filled in.</summary>
    private sealed class Arrays
    {
        public int[] CodePointStarts { get; init; } = [];

        public int[] CodePointSpans { get; init; } = [];

        public int[] CodePointAt { get; init; } = [];

        public int[] GlyphStarts { get; init; } = [];

        public PreparedGlyph[] Glyphs { get; init; } = [];

        public bool[] ClusterStarts { get; set; } = [];

        public int[] ClusterFirst { get; set; } = [];

        public ClusterKind[] ClusterKinds { get; set; } = [];

        public double[] ClusterSpacing { get; set; } = [];

        public LineBreakKind[] BreakBefore { get; set; } = [];

        public int[] NextBreak { get; set; } = [];

        public double[] PrefixWidth { get; set; } = [];

        public int[] LastDrawnBefore { get; set; } = [];

        public int[] ContentEnd { get; set; } = [];

        public double[] NegativeAfter { get; set; } = [];

        public PreparedGlyph?[] SpanHyphens { get; set; } = [];

        public double[] SpanAbove { get; set; } = [];

        public double[] SpanBelow { get; set; } = [];

        public (double Top, double Thickness)[] SpanUnderlines { get; set; } = [];

        public (double Top, double Thickness)[] SpanStrikethroughs { get; set; } = [];
    }
}
