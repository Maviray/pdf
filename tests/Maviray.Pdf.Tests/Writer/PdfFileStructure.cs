using System.Globalization;
using System.Text;

namespace Maviray.Pdf.Tests.Writer;

/// <summary>
/// Reads back the structure the file writer produced: the <c>startxref</c> offset, the cross-reference entries and the
/// trailer. It asserts the fixed layout while it parses: 20-byte entries ending with a space and LF, and the free entry.
/// </summary>
internal sealed class PdfFileStructure
{
    private PdfFileStructure(string text, long startXref, IReadOnlyList<long> offsets, string trailer)
    {
        Text = text;
        StartXref = startXref;
        Offsets = offsets;
        Trailer = trailer;
    }

    /// <summary>The whole file as Latin-1 text: one character per byte.</summary>
    public string Text { get; }

    public long StartXref { get; }

    /// <summary>The byte offset of each object, indexed by object number. Index 0, the free entry, holds -1.</summary>
    public IReadOnlyList<long> Offsets { get; }

    /// <summary>The trailer dictionary, without the <c>trailer</c> keyword.</summary>
    public string Trailer { get; }

    public static PdfFileStructure Parse(byte[] file)
    {
        var text = Encoding.Latin1.GetString(file);

        var startXrefKeyword = text.LastIndexOf("startxref\n", StringComparison.Ordinal);
        Assert.True(startXrefKeyword >= 0, "The file has no startxref.");
        var numberStart = startXrefKeyword + "startxref\n".Length;
        var numberEnd = text.IndexOf('\n', numberStart);
        var startXref = long.Parse(text[numberStart..numberEnd], CultureInfo.InvariantCulture);
        Assert.Equal("%%EOF\n", text[(numberEnd + 1)..]);

        var xref = checked((int)startXref);
        Assert.StartsWith("xref\n0 ", text[xref..], StringComparison.Ordinal);
        var headerEnd = text.IndexOf('\n', xref + "xref\n".Length);
        var count = int.Parse(text[(xref + "xref\n0 ".Length)..headerEnd], CultureInfo.InvariantCulture);

        var offsets = new List<long>();
        var position = headerEnd + 1;
        for (var i = 0; i < count; i++)
        {
            var entry = text.Substring(position, 20);
            if (i == 0)
            {
                Assert.Equal("0000000000 65535 f \n", entry);
                offsets.Add(-1);
            }
            else
            {
                Assert.Equal(" 00000 n \n", entry[10..]);
                offsets.Add(long.Parse(entry[..10], CultureInfo.InvariantCulture));
            }

            position += 20;
        }

        Assert.StartsWith("trailer\n", text[position..], StringComparison.Ordinal);
        var trailer = text[(position + "trailer\n".Length)..(startXrefKeyword - 1)];

        return new PdfFileStructure(text, startXref, offsets, trailer);
    }

    /// <summary>The text of object <paramref name="number"/>, from its offset to the end of <c>endobj</c>.</summary>
    public string ObjectText(int number)
    {
        var start = checked((int)Offsets[number]);
        var end = Text.IndexOf("\nendobj\n", start, StringComparison.Ordinal);
        return Text[start..(end + "\nendobj\n".Length)];
    }
}
