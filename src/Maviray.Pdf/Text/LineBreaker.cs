using static Maviray.Pdf.Text.LineBreakClass;

namespace Maviray.Pdf.Text;

/// <summary>
/// The default line-breaking algorithm of UAX #14 for Unicode 16.0, rules LB1 to LB31, with no tailoring (spec T-06).
/// Each boundary takes the first rule that matches. Rules LB11 to LB31 see items: a code point with every CM and ZWJ
/// that LB9 attaches to it, which have the class of their base. Every look back over a run (spaces, SY and IS, regional
/// indicators) reads a value computed once per item, so the work is linear in the length of the text.
/// </summary>
internal static class LineBreaker
{
    private const int DottedCircle = 0x25CC;
    private const int Hyphen = 0x2010;

    /// <summary>
    /// The kind of each UTF-16 boundary of <paramref name="text"/>: entry <c>i</c> is the boundary before
    /// <c>text[i]</c>. Entry 0 is <see cref="LineBreakKind.None"/> (LB2), and the last entry is
    /// <see cref="LineBreakKind.Required"/> (LB3), except for empty text. A boundary inside a surrogate pair is
    /// <see cref="LineBreakKind.None"/>. A lone surrogate is read as its own code unit, class SG, so AL.
    /// </summary>
    public static LineBreakKind[] FindBreaks(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var kinds = new LineBreakKind[text.Length + 1];
        if (text.Length == 0)
        {
            return kinds;
        }

        var items = new Items(text);
        for (var j = 1; j < items.Count; j++)
        {
            kinds[items.Start[j]] = Decide(items, j);
        }

        // LB9: no break before a CM or ZWJ that joined an item. The array already holds None there.
        kinds[text.Length] = LineBreakKind.Required;
        return kinds;
    }

    /// <summary>The kind of the boundary between items <c>j - 1</c> and <c>j</c>.</summary>
    private static LineBreakKind Decide(Items items, int j)
    {
        var a = items.Class[j - 1];
        var b = items.Class[j];
        var last = items.LastRaw[j - 1];

        // LB4, LB5: break characters.
        if (last == BK)
        {
            return LineBreakKind.Required;
        }

        if (last == CR && b == LF)
        {
            return LineBreakKind.None;
        }

        if (last is CR or LF or NL)
        {
            return LineBreakKind.Required;
        }

        // LB6, LB7.
        if (b is BK or CR or LF or NL or SP or ZW)
        {
            return LineBreakKind.None;
        }

        // LB8: ZW SP* ÷.
        var beforeSpaces = items.BeforeSpaces[j];
        if (beforeSpaces >= 0 && items.Class[beforeSpaces] == ZW)
        {
            return LineBreakKind.Allowed;
        }

        // LB8a: ZWJ ×, looking at the last code point of the item, which may be an attached ZWJ.
        if (last == ZWJ)
        {
            return LineBreakKind.None;
        }

        return Rule11To31(items, j, a, b, beforeSpaces) ? LineBreakKind.None : LineBreakKind.Allowed;
    }

    /// <summary>Whether one of LB11 to LB30b forbids the break; otherwise LB31 allows it.</summary>
    private static bool Rule11To31(Items items, int j, LineBreakClass a, LineBreakClass b, int beforeSpaces)
    {
        var p = j - 1;
        var classBefore = beforeSpaces >= 0 ? items.Class[beforeSpaces] : (LineBreakClass?)null;
        var hasNext = j + 1 < items.Count;
        var next = hasNext ? items.Class[j + 1] : (LineBreakClass?)null;

        // LB11, LB12, LB12a, LB13.
        if (b == WJ || a == WJ || a == GL)
        {
            return true;
        }

        if (b == GL && a is not (SP or BA or HY))
        {
            return true;
        }

        if (b is CL or CP or EX or SY)
        {
            return true;
        }

        // LB14: OP SP* ×.
        if (classBefore == OP)
        {
            return true;
        }

        // LB15a: (sot | BK | CR | LF | NL | OP | QU | GL | SP | ZW) [QU & Pi] SP* ×.
        if (beforeSpaces >= 0 && items.IsInitialQuote(beforeSpaces) &&
            (beforeSpaces == 0 || items.Class[beforeSpaces - 1] is BK or CR or LF or NL or OP or QU or GL or SP or ZW))
        {
            return true;
        }

        // LB15b: × [QU & Pf] (SP | GL | WJ | CL | QU | CP | EX | IS | SY | BK | CR | LF | NL | ZW | eot).
        if (items.IsFinalQuote(j) &&
            (!hasNext || next is SP or GL or WJ or CL or QU or CP or EX or IS or SY or BK or CR or LF or NL or ZW))
        {
            return true;
        }

        // LB15c: SP ÷ IS NU. LB15d: × IS.
        if (a == SP && b == IS && next == NU)
        {
            return false;
        }

        if (b == IS)
        {
            return true;
        }

        // LB16: (CL | CP) SP* × NS. LB17: B2 SP* × B2.
        if (b == NS && classBefore is CL or CP)
        {
            return true;
        }

        if (b == B2 && classBefore == B2)
        {
            return true;
        }

        // LB18: SP ÷.
        if (a == SP)
        {
            return false;
        }

        // LB19: × [QU - Pi], [QU - Pf] ×.
        if ((b == QU && !items.IsInitialQuote(j)) || (a == QU && !items.IsFinalQuote(p)))
        {
            return true;
        }

        // LB19a: quotation marks next to anything but East Asian.
        if (b == QU && (!items.IsEastAsian(p) || !hasNext || !items.IsEastAsian(j + 1)))
        {
            return true;
        }

        if (a == QU && (!items.IsEastAsian(j) || p == 0 || !items.IsEastAsian(p - 1)))
        {
            return true;
        }

        // LB20: ÷ CB, CB ÷.
        if (a == CB || b == CB)
        {
            return false;
        }

        // LB20a: (sot | BK | CR | LF | NL | SP | ZW | CB | GL) (HY | U+2010) × AL.
        if ((a == HY || items.Base[p] == Hyphen) && b == AL &&
            (p == 0 || items.Class[p - 1] is BK or CR or LF or NL or SP or ZW or CB or GL))
        {
            return true;
        }

        // LB21: × BA, × HY, × NS, BB ×.
        if (b is BA or HY or NS || a == BB)
        {
            return true;
        }

        // LB21a: HL (HY | [BA - $EA]) × [^HL]. LB21b: SY × HL. LB22: × IN.
        if (p >= 1 && items.Class[p - 1] == HL && (a == HY || (a == BA && !items.IsEastAsian(p))) && b != HL)
        {
            return true;
        }

        if ((a == SY && b == HL) || b == IN)
        {
            return true;
        }

        // LB23, LB23a, LB24.
        if ((a is AL or HL && b == NU) || (a == NU && b is AL or HL))
        {
            return true;
        }

        if ((a == PR && b is ID or EB or EM) || (a is ID or EB or EM && b == PO))
        {
            return true;
        }

        if ((a is PR or PO && b is AL or HL) || (a is AL or HL && b is PR or PO))
        {
            return true;
        }

        if (Rule25(items, j, a, b))
        {
            return true;
        }

        // LB26, LB27.
        if ((a == JL && b is JL or JV or H2 or H3) || (a is JV or H2 && b is JV or JT) || (a is JT or H3 && b == JT))
        {
            return true;
        }

        if ((a is JL or JV or JT or H2 or H3 && b == PO) || (a == PR && b is JL or JV or JT or H2 or H3))
        {
            return true;
        }

        // LB28: (AL | HL) × (AL | HL).
        if (a is AL or HL && b is AL or HL)
        {
            return true;
        }

        if (Rule28a(items, j, a, b))
        {
            return true;
        }

        // LB29: IS × (AL | HL). LB30: (AL | HL | NU) × [OP - $EA], [CP - $EA] × (AL | HL | NU).
        if (a == IS && b is AL or HL)
        {
            return true;
        }

        if ((a is AL or HL or NU && b == OP && !items.IsEastAsian(j)) ||
            (a == CP && !items.IsEastAsian(p) && b is AL or HL or NU))
        {
            return true;
        }

        // LB30a: an odd number of regional indicators before the boundary pairs with the one after it.
        if (a == RI && b == RI && items.RegionalRun[j] % 2 == 1)
        {
            return true;
        }

        // LB30b: EB × EM, [Extended_Pictographic & Cn] × EM.
        return b == EM && (a == EB || LineBreakProperties.IsUnassignedPictographic(items.Base[p]));
    }

    /// <summary>
    /// LB25, the numbers: <c>NU (SY | IS)* (CL | CP)? × (PO | PR)</c>, <c>(PO | PR) × OP IS? NU</c>,
    /// <c>(PO | PR | HY | IS) × NU</c> and <c>NU (SY | IS)* × NU</c>.
    /// </summary>
    private static bool Rule25(Items items, int j, LineBreakClass a, LineBreakClass b)
    {
        if (b is PO or PR)
        {
            var start = a is CL or CP ? items.BeforeSyIs[j - 1] : items.BeforeSyIs[j];
            return start >= 0 && items.Class[start] == NU;
        }

        if (a is PO or PR && b == OP && j + 1 < items.Count)
        {
            var next = items.Class[j + 1];
            return next == NU || (next == IS && j + 2 < items.Count && items.Class[j + 2] == NU);
        }

        if (b == NU)
        {
            if (a is PO or PR or HY or IS)
            {
                return true;
            }

            var start = items.BeforeSyIs[j];
            return start >= 0 && items.Class[start] == NU;
        }

        return false;
    }

    /// <summary>
    /// LB28a, the Brahmic orthographic syllables, with ◌ for U+25CC: <c>AP × (AK | ◌ | AS)</c>,
    /// <c>(AK | ◌ | AS) × (VF | VI)</c>, <c>(AK | ◌ | AS) VI × (AK | ◌)</c> and
    /// <c>(AK | ◌ | AS) × (AK | ◌ | AS) VF</c>.
    /// </summary>
    private static bool Rule28a(Items items, int j, LineBreakClass a, LineBreakClass b)
    {
        var p = j - 1;
        if (a == AP && IsAksara(items, j))
        {
            return true;
        }

        if (IsAksara(items, p) && b is VF or VI)
        {
            return true;
        }

        if (a == VI && p >= 1 && IsAksara(items, p - 1) && (b == AK || items.Base[j] == DottedCircle))
        {
            return true;
        }

        return IsAksara(items, p) && IsAksara(items, j) && j + 1 < items.Count && items.Class[j + 1] == VF;
    }

    private static bool IsAksara(Items items, int item) =>
        items.Class[item] is AK or AS || items.Base[item] == DottedCircle;

    /// <summary>
    /// The text as LB9 items, with what the rules look back at. All arrays are indexed by item.
    /// </summary>
    private sealed class Items
    {
        public Items(string text)
        {
            Start = new int[text.Length];
            Base = new int[text.Length];
            Class = new LineBreakClass[text.Length];
            LastRaw = new LineBreakClass[text.Length];

            for (var i = 0; i < text.Length;)
            {
                int codePoint = text[i];
                var width = 1;
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    codePoint = char.ConvertToUtf32(text[i], text[i + 1]);
                    width = 2;
                }

                var raw = LineBreakProperties.Class(codePoint);

                // LB9: a CM or ZWJ joins the item before it, unless that item is BK, CR, LF, NL, SP or ZW.
                if (raw is CM or ZWJ && Count > 0 && Class[Count - 1] is not (BK or CR or LF or NL or SP or ZW))
                {
                    LastRaw[Count - 1] = raw;
                }
                else
                {
                    Start[Count] = i;
                    Base[Count] = codePoint;

                    // LB10: a CM or ZWJ that joined nothing is AL.
                    Class[Count] = raw is CM or ZWJ ? AL : raw;
                    LastRaw[Count] = raw;
                    Count++;
                }

                i += width;
            }

            BeforeSpaces = new int[Count];
            BeforeSyIs = new int[Count];
            RegionalRun = new int[Count];
            BeforeSpaces[0] = -1;
            BeforeSyIs[0] = -1;
            for (var j = 1; j < Count; j++)
            {
                var previous = Class[j - 1];
                BeforeSpaces[j] = previous == SP ? BeforeSpaces[j - 1] : j - 1;
                BeforeSyIs[j] = previous is SY or IS ? BeforeSyIs[j - 1] : j - 1;
                RegionalRun[j] = previous == RI ? RegionalRun[j - 1] + 1 : 0;
            }
        }

        public int Count { get; }

        /// <summary>The UTF-16 index where each item starts.</summary>
        public int[] Start { get; }

        /// <summary>The code point of each item's base.</summary>
        public int[] Base { get; }

        /// <summary>The class each item has in rules LB11 to LB31: its base's, AL for a lone CM or ZWJ (LB10).</summary>
        public LineBreakClass[] Class { get; }

        /// <summary>The class of each item's last code point, as LB4, LB5 and LB8a see it.</summary>
        public LineBreakClass[] LastRaw { get; }

        /// <summary>The last item before item j that isn't SP, or −1.</summary>
        public int[] BeforeSpaces { get; }

        /// <summary>The last item before item j that is neither SY nor IS, or −1.</summary>
        public int[] BeforeSyIs { get; }

        /// <summary>How many regional indicators come right before item j.</summary>
        public int[] RegionalRun { get; }

        public bool IsEastAsian(int item) => LineBreakProperties.IsEastAsian(Base[item]);

        public bool IsInitialQuote(int item) => LineBreakProperties.IsInitialQuote(Base[item]);

        public bool IsFinalQuote(int item) => LineBreakProperties.IsFinalQuote(Base[item]);
    }
}
