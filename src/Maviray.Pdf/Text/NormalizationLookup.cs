using Maviray.Pdf.Text.Unicode;

namespace Maviray.Pdf.Text;

/// <summary>
/// Reads the generated normalization tables (spec T-02). Every lookup takes any <see cref="int"/> and never fails: a
/// value that isn't a code point, or an unassigned one, gets class 0, quick check Yes, no decomposition and no
/// composite, which are the UCD's defaults.
/// </summary>
internal static class NormalizationLookup
{
    /// <summary>The longest full canonical decomposition of a single code point, in code points.</summary>
    public const int MaxDecompositionLength = 4;

    private const int MaxCodePoint = 0x10FFFF;
    private const int BlockMask = (1 << NfcPropertyData.BlockShift) - 1;

    /// <summary>The canonical combining class, 0 to 254.</summary>
    public static int CombiningClass(int codePoint) => Entry(codePoint) & 0xFF;

    /// <summary>The value of <c>NFC_Quick_Check</c>.</summary>
    public static NfcQuickCheck QuickCheck(int codePoint) => (NfcQuickCheck)((Entry(codePoint) >> 8) & 0b11);

    /// <summary>
    /// Writes the full canonical decomposition of the code point into <paramref name="destination"/>, which holds at
    /// least <see cref="MaxDecompositionLength"/> values: the jamo of a Hangul syllable, the table's mapping, or the
    /// code point itself. Returns how many code points it wrote, 1 to 4.
    /// </summary>
    public static int Decompose(int codePoint, Span<int> destination)
    {
        if (Hangul.IsSyllable(codePoint))
        {
            return Hangul.Decompose(codePoint, destination);
        }

        var index = DecompositionData.CodePoints.BinarySearch(codePoint);
        if (index < 0)
        {
            destination[0] = codePoint;
            return 1;
        }

        var starts = DecompositionData.Starts;
        var mapping = DecompositionData.Mappings[starts[index]..starts[index + 1]];
        mapping.CopyTo(destination);
        return mapping.Length;
    }

    /// <summary>The primary composite of the two code points, Hangul syllables included, or −1 when there is none.</summary>
    public static int Compose(int first, int second)
    {
        var syllable = Hangul.Compose(first, second);
        if (syllable >= 0)
        {
            return syllable;
        }

        if ((uint)first > MaxCodePoint || (uint)second > MaxCodePoint)
        {
            return -1;
        }

        var index = CompositionData.Pairs.BinarySearch(((ulong)(uint)first << 32) | (uint)second);
        return index >= 0 ? CompositionData.Composites[index] : -1;
    }

    private static int Entry(int codePoint)
    {
        if ((uint)codePoint > MaxCodePoint)
        {
            return 0;
        }

        int block = NfcPropertyData.BlockIndex[codePoint >> NfcPropertyData.BlockShift];
        return NfcPropertyData.Blocks[(block << NfcPropertyData.BlockShift) | (codePoint & BlockMask)];
    }
}
