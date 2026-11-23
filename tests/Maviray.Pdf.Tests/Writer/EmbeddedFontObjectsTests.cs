using System.IO.Compression;
using System.Text;
using Maviray.Pdf.Fonts;
using Maviray.Pdf.Tests.Fonts;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

/// <summary>
/// The objects an embedded font writes when its document closes (spec F-09, F-11): <c>FontFile2</c>, the font
/// descriptor, the CIDFont, ToUnicode and, at the reserved reference, the Type 0 font, in that order. A font that may
/// not be subset adds a <c>CIDToGIDMap</c> stream before the CIDFont. The default test font maps space, A and B to
/// glyphs 1, 2 and 3, with advances 250, 600 and 650; <c>.notdef</c>'s is 500.
/// </summary>
public sealed class EmbeddedFontObjectsTests
{
    private static readonly (ushort Glyph, int CodePoint)[] Pairs = [(2, 'A'), (3, 'B'), (1, ' ')];

    [Fact]
    public void WritesTheFiveObjects()
    {
        var file = WriteFont(new TestFontBuilder().Build(), Pairs);

        Assert.Equal(6, file.Offsets.Count); // the free entry and five objects
        Assert.Equal(
            "<</Type /Font /Subtype /Type0 /BaseFont /WVQWOF+TestFont-Regular /Encoding /Identity-H /DescendantFonts [4 0 R] "
            + "/ToUnicode 5 0 R>>",
            DictionaryOf(file.ObjectText(1)));
        Assert.Equal(
            "<</Type /Font /Subtype /CIDFontType2 /BaseFont /WVQWOF+TestFont-Regular "
            + "/CIDSystemInfo <</Registry (Adobe) /Ordering (Identity) /Supplement 0>> /FontDescriptor 3 0 R "
            + "/W [0 [500 600 650 250]] /CIDToGIDMap /Identity>>",
            DictionaryOf(file.ObjectText(4)));
        Assert.StartsWith("<</Type /FontDescriptor /FontName /WVQWOF+TestFont-Regular ", DictionaryOf(file.ObjectText(3)), StringComparison.Ordinal);
        Assert.Contains("/FontFile2 2 0 R>>", DictionaryOf(file.ObjectText(3)), StringComparison.Ordinal);
        Assert.Equal(ToUnicodeCMap.Build([0, 'A', 'B', ' ']), DataOf(file.ObjectText(5)));
    }

    [Fact]
    public void FontFile2HoldsTheSubset()
    {
        var bytes = new TestFontBuilder().Build();

        var file = WriteFont(bytes, Pairs);

        var subset = FontSubsetter.Build(TrueTypeFont.Load(bytes), [0, 2, 3, 1]).Data.ToArray();
        Assert.Equal(subset, DataOf(file.ObjectText(2)));
        Assert.Contains($"/Length1 {subset.Length}", DictionaryOf(file.ObjectText(2)), StringComparison.Ordinal);
    }

    [Fact]
    public void AFontThatMayNotBeSubsetIsEmbeddedWhole()
    {
        var bytes = new TestFontBuilder().FsType(0x0100).Build();

        var file = WriteFont(bytes, (2, 'A'), (3, 'B'));

        Assert.Equal(7, file.Offsets.Count); // the free entry and six objects
        Assert.Contains("/BaseFont /TestFont-Regular ", DictionaryOf(file.ObjectText(1)), StringComparison.Ordinal);
        Assert.Equal(bytes, DataOf(file.ObjectText(2)));
        Assert.Equal([0x00, 0x00, 0x00, 0x02, 0x00, 0x03], DataOf(file.ObjectText(4)));
        Assert.Contains("/FontDescriptor 3 0 R /W [0 [500 600 650]] /CIDToGIDMap 4 0 R>>", DictionaryOf(file.ObjectText(5)), StringComparison.Ordinal);
        Assert.Equal(ToUnicodeCMap.Build([0, 'A', 'B']), DataOf(file.ObjectText(6)));
    }

    [Fact]
    public void EncodeAfterWritingThrows()
    {
        using var file = new PdfFileWriter(new MemoryStream(), new PdfWriterOptions());
        var fonts = new FontResources(file);
        var font = fonts.Embed(TrueTypeFont.Load(new TestFontBuilder().Build()));
        font.Encode(2, 'A');
        fonts.NameOf(font);

        fonts.WriteAll();

        Assert.Throws<InvalidOperationException>(() => font.Encode(3, 'B'));
    }

    [Fact]
    public void WritingPastTheGlyphLimitThrows()
    {
        using var file = new PdfFileWriter(new MemoryStream(), new PdfWriterOptions());
        var fonts = new FontResources(file);
        var builder = new TestFontBuilder().CompositeGlyph(700, new TestComponent(2, 0x0002), new TestComponent(3, 0x0002));
        var font = fonts.Embed(TrueTypeFont.Load(builder.Build()));
        for (var i = 0; i < 65_534; i++)
        {
            font.Encode(4, 0x10000 + i);
        }

        fonts.NameOf(font);

        Assert.Throws<InvalidOperationException>(fonts.WriteAll);
    }

    /// <summary>Embeds the font, encodes the pairs, and closes a file holding only its objects; object 1 is the Type 0 font.</summary>
    private static PdfFileStructure WriteFont(byte[] fontFile, params (ushort Glyph, int CodePoint)[] pairs)
    {
        var destination = new MemoryStream();
        using (var file = new PdfFileWriter(destination, new PdfWriterOptions { CompressionLevel = CompressionLevel.NoCompression }))
        {
            var fonts = new FontResources(file);
            var font = fonts.Embed(TrueTypeFont.Load(fontFile));
            foreach (var (glyph, codePoint) in pairs)
            {
                font.Encode(glyph, codePoint);
            }

            fonts.NameOf(font);
            fonts.WriteAll();
            file.Close(fonts.ReferenceOf(font));
        }

        return PdfFileStructure.Parse(destination.ToArray());
    }

    private static string DictionaryOf(string objectText)
    {
        var start = objectText.IndexOf(" obj\n", StringComparison.Ordinal) + " obj\n".Length;
        var end = objectText.IndexOf("\nstream\n", start, StringComparison.Ordinal);
        return objectText[start..(end >= 0 ? end : objectText.IndexOf("\nendobj\n", start, StringComparison.Ordinal))];
    }

    private static byte[] DataOf(string objectText)
    {
        var start = objectText.IndexOf("\nstream\n", StringComparison.Ordinal) + "\nstream\n".Length;
        var end = objectText.LastIndexOf("\nendstream", StringComparison.Ordinal);
        return Encoding.Latin1.GetBytes(objectText[start..end]);
    }
}
