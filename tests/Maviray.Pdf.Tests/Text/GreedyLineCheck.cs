using System.Globalization;
using System.Text;
using Maviray.Pdf.Text;

namespace Maviray.Pdf.Tests.Text;

/// <summary>
/// Checks that a layout's lines are correct greedy lines (spec T-15), without the filler. The text is rebuilt from the
/// spans with <see cref="TextNormalizer"/>, opportunities come from <see cref="LineBreaker"/>, clusters from
/// <see cref="StringInfo"/>, and every width is measured from the fonts' advances by T-07, T-09 and T-10. The layout
/// is read only for its lines' ranges, widths and offsets, which are what this checks.
/// </summary>
/// <remarks>
/// It handles what the text gate holds: mapped characters, spaces, tabs, soft hyphens and break characters. Any other
/// control or format character is reported as outside the check, rather than measured by a guess. Blind spots:
/// <list type="bullet">
/// <item>The fit test extends a line by its next segment only, as T-15 words it. Where a line can narrow as it grows,
/// under negative letter spacing or after a soft hyphen wider than what follows it, T-09 can end the line at a later
/// segment, and this check wouldn't see a line that stopped early. The gate has neither.</item>
/// <item>A line that ends at a soft hyphen is measured with its hyphen. Where T-09 takes a whole first segment that
/// fits only without the hyphen it would draw, the check reports that correct line as too wide. The gate has no such
/// line.</item>
/// </list>
/// </remarks>
internal static class GreedyLineCheck
{
    /// <summary>T-09's fit tolerance.</summary>
    private const double Tolerance = 1e-6;

    /// <summary>How closely the layout's width and offset of a line must match the measured ones.</summary>
    private const double Exact = 1e-9;

    private const int SoftHyphen = 0x00AD;

    /// <summary>
    /// Every way <paramref name="layout"/>'s lines aren't a correct greedy fill of its paragraph at
    /// <paramref name="width"/>, each naming its line. Empty when every line is right.
    /// </summary>
    public static IReadOnlyList<string> Problems(TextLayout layout, double width)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return new Checker(layout, width).Run();
    }

    private sealed class Checker
    {
        private readonly TextLayout _layout;
        private readonly double _width;
        private readonly IReadOnlyList<TextSpan> _spans;
        private readonly List<string> _problems = [];
        private string _text = "";
        private int[] _spanOf = [];
        private LineBreakKind[] _breaks = [];
        private bool[] _clusterStarts = [];

        public Checker(TextLayout layout, double width)
        {
            _layout = layout;
            _width = width;
            _spans = layout.Source.Source.Spans;
        }

        public List<string> Run()
        {
            if (!BuildText())
            {
                return _problems;
            }

            CheckCharacters();
            _breaks = LineBreaker.FindBreaks(_text);
            _clusterStarts = new bool[_text.Length + 1];
            for (var i = 0; i < _text.Length; i += StringInfo.GetNextTextElementLength(_text, i))
            {
                _clusterStarts[i] = true;
            }

            _clusterStarts[_text.Length] = true;

            var lines = _layout.Lines;
            var expectedStart = 0;
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var end = line.Start + line.Length;
                var name = $"Line {i} [{line.Start}, {end}) \"{_text[line.Start..end]}\"";
                if (line.Start != expectedStart)
                {
                    _problems.Add($"{name} starts at {line.Start}, not where the line before ends, at {expectedStart}.");
                }

                expectedStart = end;
                CheckLine(line, name);
            }

            if (expectedStart != _text.Length)
            {
                _problems.Add($"The lines end at {expectedStart}, not at the text's end, {_text.Length}.");
            }

            return _problems;
        }

        /// <summary>The spans' NFC texts, joined, which must be the prepared text, and each index's span.</summary>
        private bool BuildText()
        {
            var builder = new StringBuilder();
            var spanOf = new List<int>();
            for (var s = 0; s < _spans.Count; s++)
            {
                var part = TextNormalizer.ToNfc(_spans[s].Text);
                builder.Append(part);
                spanOf.AddRange(Enumerable.Repeat(s, part.Length));
            }

            _text = builder.ToString();
            _spanOf = [.. spanOf];
            if (!string.Equals(_text, _layout.Source.Text, StringComparison.Ordinal))
            {
                _problems.Add("The spans' NFC text isn't the prepared text.");
                return false;
            }

            return true;
        }

        /// <summary>Every code point is one the check measures, and its span's font maps what it draws.</summary>
        private void CheckCharacters()
        {
            for (var i = 0; i < _text.Length; i += char.IsSurrogatePair(_text, i) ? 2 : 1)
            {
                var codePoint = char.ConvertToUtf32(_text, i);
                if (codePoint == SoftHyphen || IsBreakCharacter(codePoint))
                {
                    continue;
                }

                var category = CharUnicodeInfo.GetUnicodeCategory(codePoint);
                if (codePoint != '\t' && category is UnicodeCategory.Control or UnicodeCategory.Format)
                {
                    _problems.Add($"U+{codePoint:X4} at {i} is outside what the check handles.");
                }
                else if (_spans[_spanOf[i]].Style.Font.GetGlyphId(codePoint == '\t' ? ' ' : codePoint) == 0)
                {
                    _problems.Add($"U+{codePoint:X4} at {i} isn't mapped by its span's font.");
                }
            }
        }

        private void CheckLine(TextLine line, string name)
        {
            var start = line.Start;
            var end = line.Start + line.Length;
            for (var b = start + 1; b < end; b++)
            {
                if (_breaks[b] == LineBreakKind.Required)
                {
                    _problems.Add($"{name} holds a required break before its end, at {b}.");
                }
            }

            if (end > _text.Length || !_clusterStarts[end])
            {
                _problems.Add($"{name} doesn't end between clusters.");
                return;
            }

            var measured = Measure(start, end, hyphen: true);
            if (Math.Abs(measured - line.Width) > Exact)
            {
                _problems.Add($"{name} is {line.Width} wide, but measures {measured}.");
            }

            if (!Fits(measured) && NextCluster(start) < end)
            {
                _problems.Add($"{name} measures {measured}, which doesn't fit {_width}.");
            }

            if (IsOpportunity(end))
            {
                if (end < _text.Length && _breaks[end] != LineBreakKind.Required)
                {
                    var next = NextOpportunity(end);
                    var longer = Measure(start, next, hyphen: true);
                    if (Fits(longer))
                    {
                        _problems.Add($"{name} could hold the next segment, to {next}: it measures {longer}, which fits {_width}.");
                    }
                }
            }
            else
            {
                // A cut, allowed only inside a first segment that doesn't fit even without its hyphen (T-09).
                var first = NextOpportunity(start);
                if (end >= first)
                {
                    _problems.Add($"{name} ends at {end}, which is no break opportunity, after its first segment ends at {first}.");
                }
                else if (Fits(Measure(start, first, hyphen: false)))
                {
                    _problems.Add($"{name} is cut at {end}, but its first segment, to {first}, fits.");
                }

                var longer = Measure(start, NextCluster(end), hyphen: true);
                if (Fits(longer))
                {
                    _problems.Add($"{name} is cut at {end}, but one more cluster fits: it measures {longer}.");
                }
            }

            var offset = double.IsInfinity(_width) || measured > _width
                ? 0
                : _layout.Source.Source.Alignment switch
                {
                    TextAlignment.Center => (_width - measured) / 2,
                    TextAlignment.Right => _width - measured,
                    _ => 0,
                };
            if (Math.Abs(line.Offset - offset) > Exact)
            {
                _problems.Add($"{name} has offset {line.Offset}, but its alignment puts it at {offset}.");
            }
        }

        /// <summary>
        /// The width of a line over [<paramref name="start"/>, <paramref name="end"/>) as drawn (T-09, T-10): no
        /// trailing spaces, tabs or break characters, and no soft hyphen except one at the end that draws a hyphen,
        /// when <paramref name="hyphen"/> allows it. Each drawn cluster's letter spacing follows it, except the last's.
        /// </summary>
        private double Measure(int start, int end, bool hyphen)
        {
            var last = end;
            while (last > start && (_text[last - 1] is ' ' or '\t' || IsBreakCharacter(_text[last - 1])))
            {
                last--;
            }

            var total = 0.0;
            var spacing = 0.0;
            var anyDrawn = false;
            for (var cluster = start; cluster < last; cluster = NextCluster(cluster))
            {
                var advance = 0.0;
                var drawn = false;
                var clusterEnd = Math.Min(NextCluster(cluster), last);
                for (var i = cluster; i < clusterEnd; i += char.IsSurrogatePair(_text, i) ? 2 : 1)
                {
                    var codePoint = char.ConvertToUtf32(_text, i);
                    var style = _spans[_spanOf[i]].Style;
                    var font = style.Font;
                    if (codePoint == SoftHyphen)
                    {
                        if (hyphen && i == end - 1 && end < _text.Length && anyDrawn)
                        {
                            var glyph = font.GetGlyphId(0x2010) is var h and not 0 ? h : font.GetGlyphId('-');
                            advance += font.GetAdvanceWidth(glyph) * style.Size / font.UnitsPerEm;
                            drawn = true;
                        }

                        continue;
                    }

                    if (IsBreakCharacter(codePoint))
                    {
                        continue;
                    }

                    var id = font.GetGlyphId(codePoint == '\t' ? ' ' : codePoint);
                    advance += font.GetAdvanceWidth(id) * style.Size / font.UnitsPerEm;
                    drawn = true;
                }

                if (drawn)
                {
                    total += spacing + advance;
                    spacing = _spans[_spanOf[cluster]].Style.LetterSpacing;
                    anyDrawn = true;
                }
            }

            return total;
        }

        private bool Fits(double width) => width <= _width + Tolerance;

        private bool IsOpportunity(int boundary) => _clusterStarts[boundary] && _breaks[boundary] != LineBreakKind.None;

        private int NextOpportunity(int after)
        {
            for (var b = after + 1; b < _text.Length; b++)
            {
                if (IsOpportunity(b))
                {
                    return b;
                }
            }

            return _text.Length;
        }

        private int NextCluster(int after)
        {
            var b = after + 1;
            while (b < _text.Length && !_clusterStarts[b])
            {
                b++;
            }

            return Math.Min(b, _text.Length);
        }

        private static bool IsBreakCharacter(int codePoint) => codePoint is >= 0x000A and <= 0x000D or 0x0085 or 0x2028 or 0x2029;
    }
}
