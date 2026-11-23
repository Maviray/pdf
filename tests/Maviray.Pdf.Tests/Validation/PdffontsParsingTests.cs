using Xunit.Sdk;

namespace Maviray.Pdf.Tests.Validation;

/// <summary>
/// Reading pdffonts' table without the tool (spec F-13). poppler prints each row with
/// <c>printf("%-36s %-17s %-16s %-3s %-3s %-3s %6d %2d\n")</c>, which pads a name but never cuts it, so a long name
/// pushes the later columns to the right. xpdf's pdffonts has no <c>encoding</c> column. poppler reports problems in a
/// font's dictionaries on stderr while still exiting with 0.
/// </summary>
public sealed class PdffontsParsingTests
{
    private const string PopplerHeader =
        "name                                 type              encoding         emb sub uni object ID\n"
        + "------------------------------------ ----------------- ---------------- --- --- --- ---------\n";

    private const string XpdfHeader =
        "name                                 type              emb sub uni object ID\n"
        + "------------------------------------ ----------------- --- --- --- ---------\n";

    [Fact]
    public void ReadsPopplersTable()
    {
        var output = PopplerHeader
            + Row("WVQWOF+NotoSans-Regular", "CID TrueType", "Identity-H", "yes", "yes", "yes", 5)
            + Row("Helvetica", "Type 1", "WinAnsi", "no", "no", "no", 9);

        Assert.Equal(
            [new PdfFontReport("WVQWOF+NotoSans-Regular", "CID TrueType", true, true, true), new PdfFontReport("Helvetica", "Type 1", false, false, false)],
            Poppler.ReadFonts(new ToolResult(0, output, string.Empty), "test.pdf"));
    }

    [Fact]
    public void ReadsANameLongerThanItsColumn()
    {
        const string name = "ABCDEF+DejaVuSansCondensed-BoldOblique"; // 38 characters, two past the column
        var output = PopplerHeader + Row(name, "CID TrueType", "Identity-H", "yes", "no", "yes", 5);

        Assert.Equal(
            [new PdfFontReport(name, "CID TrueType", true, false, true)],
            Poppler.ReadFonts(new ToolResult(0, output, string.Empty), "test.pdf"));
    }

    [Fact]
    public void ReadsXpdfsTableWithoutAnEncodingColumn()
    {
        var output = XpdfHeader + $"{"WVQWOF+NotoSans-Regular",-36} {"CID TrueType",-17} {"yes",-3} {"no",-3} {"yes",-3} {5,6} {0,2}\n";

        Assert.Equal(
            [new PdfFontReport("WVQWOF+NotoSans-Regular", "CID TrueType", true, false, true)],
            Poppler.ReadFonts(new ToolResult(0, output, string.Empty), "test.pdf"));
    }

    [Fact]
    public void FailsOnAnythingOnStandardError()
    {
        var output = PopplerHeader + Row("WVQWOF+NotoSans-Regular", "CID TrueType", "Identity-H", "yes", "yes", "yes", 5);
        var result = new ToolResult(0, output, "Syntax Error: Illegal entry in bfchar block in ToUnicode CMap\n");

        Assert.ThrowsAny<XunitException>(() => Poppler.ReadFonts(result, "test.pdf"));
    }

    [Fact]
    public void FailsOnANonZeroExitCode()
    {
        Assert.ThrowsAny<XunitException>(() => Poppler.ReadFonts(new ToolResult(1, string.Empty, "I/O Error"), "test.pdf"));
    }

    private static string Row(string name, string type, string encoding, string embedded, string subset, string unicode, int number) =>
        $"{name,-36} {type,-17} {encoding,-16} {embedded,-3} {subset,-3} {unicode,-3} {number,6} {0,2}\n";
}
