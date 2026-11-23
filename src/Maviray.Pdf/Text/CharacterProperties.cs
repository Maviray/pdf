using Maviray.Pdf.Text.Unicode;

namespace Maviray.Pdf.Text;

/// <summary>
/// Reads the generated character table: the properties the glyph rules of T-07 test. Every lookup takes any
/// <see cref="int"/> and never fails: a value that isn't a code point has none of them.
/// </summary>
internal static class CharacterProperties
{
    private const int MaxCodePoint = 0x10FFFF;
    private const int BlockMask = (1 << CharacterData.BlockShift) - 1;
    private const int ControlFlag = 1;
    private const int SpaceSeparatorFlag = 1 << 1;
    private const int DefaultIgnorableFlag = 1 << 2;

    /// <summary>Whether the general category is Cc, a control.</summary>
    public static bool IsControl(int codePoint) => Has(codePoint, ControlFlag);

    /// <summary>Whether the general category is Zs, a space separator such as U+00A0.</summary>
    public static bool IsSpaceSeparator(int codePoint) => Has(codePoint, SpaceSeparatorFlag);

    /// <summary>Whether the code point is <c>Default_Ignorable_Code_Point</c>, such as U+200B or U+FEFF.</summary>
    public static bool IsDefaultIgnorable(int codePoint) => Has(codePoint, DefaultIgnorableFlag);

    private static bool Has(int codePoint, int flag)
    {
        if ((uint)codePoint > MaxCodePoint)
        {
            return false;
        }

        int block = CharacterData.BlockIndex[codePoint >> CharacterData.BlockShift];
        return (CharacterData.Blocks[(block << CharacterData.BlockShift) | (codePoint & BlockMask)] & flag) != 0;
    }
}
