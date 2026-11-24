namespace Maviray.Pdf.Text;

/// <summary>
/// The arithmetic decomposition and composition of Hangul syllables, from the Unicode Standard 3.12. The Unicode
/// tables leave the 11,172 precomposed syllables out, because their jamo follow from the code point.
/// </summary>
internal static class Hangul
{
    public const int SBase = 0xAC00;
    public const int LBase = 0x1100;
    public const int VBase = 0x1161;
    public const int TBase = 0x11A7;
    public const int LCount = 19;
    public const int VCount = 21;
    public const int TCount = 28;
    public const int NCount = VCount * TCount;
    public const int SCount = LCount * NCount;

    public static bool IsSyllable(int codePoint) => (uint)(codePoint - SBase) < SCount;

    /// <summary>Writes the syllable's two or three jamo, L V or L V T, and returns how many it wrote.</summary>
    public static int Decompose(int syllable, Span<int> destination)
    {
        var index = syllable - SBase;
        destination[0] = LBase + (index / NCount);
        destination[1] = VBase + (index % NCount / TCount);
        var t = index % TCount;
        if (t == 0)
        {
            return 2;
        }

        destination[2] = TBase + t;
        return 3;
    }

    /// <summary>The LV syllable of an L and a V jamo, the LVT syllable of an LV syllable and a T jamo, or −1.</summary>
    public static int Compose(int first, int second)
    {
        var l = first - LBase;
        var v = second - VBase;
        if ((uint)l < LCount && (uint)v < VCount)
        {
            return SBase + (((l * VCount) + v) * TCount);
        }

        var s = first - SBase;
        var t = second - TBase;
        if ((uint)s < SCount && s % TCount == 0 && t > 0 && t < TCount)
        {
            return first + t;
        }

        return -1;
    }
}
