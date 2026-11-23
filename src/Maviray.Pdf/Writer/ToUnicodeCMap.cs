using System.Globalization;
using System.Text;

namespace Maviray.Pdf.Writer;

/// <summary>
/// The ToUnicode CMap of an embedded font (spec F-09): it maps each CID to its character, so text copies out and
/// searches exactly. It follows the standard form of Adobe's ToUnicode mapping files: one two-byte code space,
/// <c>bfchar</c> entries in CID order, at most 100 per block as the format allows, and UTF-16BE destinations, so a
/// character above U+FFFF is its surrogate pair. CID 0, <c>.notdef</c>, has no entry.
/// </summary>
internal static class ToUnicodeCMap
{
    private const int EntriesPerBlock = 100;

    private const string Header =
        "/CIDInit /ProcSet findresource begin\n"
        + "12 dict begin\n"
        + "begincmap\n"
        + "/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n"
        + "/CMapName /Adobe-Identity-UCS def\n"
        + "/CMapType 2 def\n"
        + "1 begincodespacerange\n"
        + "<0000> <FFFF>\n"
        + "endcodespacerange\n";

    private const string Footer =
        "endcmap\n"
        + "CMapName currentdict /CMapResource defineresource pop\n"
        + "end\n"
        + "end\n";

    /// <param name="codePoints">The character of each CID, from CID 0, whose entry is ignored. Each is a Unicode
    /// scalar value.</param>
    /// <returns>The CMap as ASCII, every line ending with LF.</returns>
    public static byte[] Build(IReadOnlyList<int> codePoints)
    {
        ArgumentNullException.ThrowIfNull(codePoints);
        var text = new StringBuilder(Header);
        Span<char> utf16 = stackalloc char[2];
        for (var first = 1; first < codePoints.Count; first += EntriesPerBlock)
        {
            var count = Math.Min(EntriesPerBlock, codePoints.Count - first);
            text.Append(CultureInfo.InvariantCulture, $"{count} beginbfchar\n");
            for (var cid = first; cid < first + count; cid++)
            {
                text.Append(CultureInfo.InvariantCulture, $"<{cid:X4}> <");
                var length = new Rune(codePoints[cid]).EncodeToUtf16(utf16);
                for (var i = 0; i < length; i++)
                {
                    text.Append(CultureInfo.InvariantCulture, $"{(int)utf16[i]:X4}");
                }

                text.Append(">\n");
            }

            text.Append("endbfchar\n");
        }

        text.Append(Footer);
        return Encoding.ASCII.GetBytes(text.ToString());
    }
}
