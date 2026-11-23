using System.Globalization;
using System.Text;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

/// <summary>
/// The ToUnicode CMap that makes text copy out (spec F-09): the standard form of Adobe's ToUnicode mapping, one
/// <c>bfchar</c> entry per CID in CID order, at most 100 per block, and a surrogate pair above U+FFFF.
/// </summary>
public sealed class ToUnicodeCMapTests
{
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

    [Fact]
    public void WritesTheStandardFormWithASurrogatePair()
    {
        var text = Encoding.ASCII.GetString(ToUnicodeCMap.Build([0, 'A', 'B', 0x1F600]));

        Assert.Equal(
            Header
            + "3 beginbfchar\n"
            + "<0001> <0041>\n"
            + "<0002> <0042>\n"
            + "<0003> <D83DDE00>\n"
            + "endbfchar\n"
            + Footer,
            text);
    }

    [Fact]
    public void SplitsIntoBlocksOf100()
    {
        int[] codePoints = [0, .. Enumerable.Range(1, 250).Select(cid => 0x4E00 + cid)];

        var text = Encoding.ASCII.GetString(ToUnicodeCMap.Build(codePoints));

        var headers = text.Split('\n').Where(line => line.EndsWith(" beginbfchar", StringComparison.Ordinal)).ToArray();
        Assert.Equal(["100 beginbfchar", "100 beginbfchar", "50 beginbfchar"], headers);
        var body = text[(text.IndexOf("endcodespacerange\n", StringComparison.Ordinal) + "endcodespacerange\n".Length)..];
        var entries = body.Split('\n').Where(line => line.StartsWith('<')).ToArray();
        Assert.Equal(250, entries.Length);
        Assert.Equal("<0001> <4E01>", entries[0]);
        Assert.Equal("<00FA> <4EFA>", entries[^1]);
        Assert.Equal(
            Enumerable.Range(1, 250).Select(cid => string.Create(CultureInfo.InvariantCulture, $"<{cid:X4}> <{0x4E00 + cid:X4}>")),
            entries);
    }

    [Fact]
    public void WithOnlyNotdefThereAreNoEntries()
    {
        var text = Encoding.ASCII.GetString(ToUnicodeCMap.Build([0]));

        Assert.Equal(Header + Footer, text);
    }
}
