using Maviray.Pdf.Text.Unicode;

namespace Maviray.Pdf.Text;

/// <summary>
/// Reads the generated line-break table (spec T-06): each code point's class after LB1 of UAX #14, and the flags its
/// rules test. Every lookup takes any <see cref="int"/> and never fails: a value that isn't a code point gets AL, which
/// is LB1's resolution of the default class XX, and no flags.
/// </summary>
internal static class LineBreakProperties
{
    private const int MaxCodePoint = 0x10FFFF;
    private const int BlockMask = (1 << LineBreakData.BlockShift) - 1;
    private const int ClassMask = 0b11_1111;
    private const int EastAsianFlag = 1 << 6;
    private const int InitialQuoteFlag = 1 << 7;
    private const int FinalQuoteFlag = 1 << 8;
    private const int UnassignedPictographicFlag = 1 << 9;

    /// <summary>The class after LB1, so never AI, SG, XX, SA or CJ.</summary>
    public static LineBreakClass Class(int codePoint) =>
        (uint)codePoint > MaxCodePoint ? LineBreakClass.AL : (LineBreakClass)(Entry(codePoint) & ClassMask);

    /// <summary>Whether the East Asian width is F, W or H, which UAX #14 calls East Asian.</summary>
    public static bool IsEastAsian(int codePoint) => Has(codePoint, EastAsianFlag);

    /// <summary>Whether the code point is a QU with general category Pi.</summary>
    public static bool IsInitialQuote(int codePoint) => Has(codePoint, InitialQuoteFlag);

    /// <summary>Whether the code point is a QU with general category Pf.</summary>
    public static bool IsFinalQuote(int codePoint) => Has(codePoint, FinalQuoteFlag);

    /// <summary>Whether the code point is Extended_Pictographic and unassigned, as LB30b tests it.</summary>
    public static bool IsUnassignedPictographic(int codePoint) => Has(codePoint, UnassignedPictographicFlag);

    private static bool Has(int codePoint, int flag) => (uint)codePoint <= MaxCodePoint && (Entry(codePoint) & flag) != 0;

    private static int Entry(int codePoint)
    {
        int block = LineBreakData.BlockIndex[codePoint >> LineBreakData.BlockShift];
        return LineBreakData.Blocks[(block << LineBreakData.BlockShift) | (codePoint & BlockMask)];
    }
}
