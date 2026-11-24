using System.Text;
using Maviray.Pdf.Fonts;
using Maviray.Pdf.Tests.Fonts;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

/// <summary>
/// Drawing an embedded font (spec F-10): <c>Tf</c> with the same state tracking as the standard font, and
/// <c>ShowGlyphs</c>, which writes CIDs as one hex string of four uppercase digits each. Refusals happen before
/// anything is written. The font has CIDs 1 (A) and 2 (B).
/// </summary>
public sealed class ContentStreamGlyphsTests : IDisposable
{
    private readonly PdfFileWriter _file = new(new MemoryStream(), new PdfWriterOptions());
    private readonly FontResources _fonts;
    private readonly EmbeddedFont _font;

    public ContentStreamGlyphsTests()
    {
        _fonts = new FontResources(_file);
        _font = _fonts.Embed(TrueTypeFont.Load(new TestFontBuilder().Build()));
        _font.Encode(2, 'A');
        _font.Encode(3, 'B');
    }

    [Fact]
    public void ShowGlyphsWritesFourHexDigitsPerCid()
    {
        using var builder = new ContentStreamBuilder(_fonts);

        builder.BeginText().SetFont(_font, 24).ShowGlyphs([1, 2, 1]).ShowGlyphs([]).EndText();

        Assert.Equal("BT\n/F1 24 Tf\n<000100020001> Tj\n<> Tj\nET\n", Finish(builder));
    }

    [Fact]
    public void ShowGlyphsRefusals()
    {
        using var builder = new ContentStreamBuilder(_fonts);

        builder.BeginText();
        Assert.Throws<InvalidOperationException>(() => builder.ShowGlyphs([1]));
        builder.SetFont(StandardFont.Helvetica, 12);
        Assert.Throws<InvalidOperationException>(() => builder.ShowGlyphs([1]));
        builder.SetFont(_font, 12);
        Assert.Throws<ArgumentException>(() => builder.ShowGlyphs([1, 0]));
        Assert.Throws<ArgumentException>(() => builder.ShowGlyphs([1, 3]));
        builder.EndText();
        Assert.Throws<InvalidOperationException>(() => builder.ShowGlyphs([1]));

        Assert.Equal("BT\n/F1 12 Tf\n/F2 12 Tf\nET\n", Finish(builder));
    }

    [Fact]
    public void ShowTextRefusesAnEmbeddedFont()
    {
        using var builder = new ContentStreamBuilder(_fonts);
        builder.BeginText().SetFont(_font, 12);

        Assert.Throws<InvalidOperationException>(() => builder.ShowText("A"));
    }

    [Fact]
    public void SetFontRefusesAFontFromAnotherDocument()
    {
        using var otherFile = new PdfFileWriter(new MemoryStream(), new PdfWriterOptions());
        var other = new FontResources(otherFile).Embed(_font.Font);
        using var builder = new ContentStreamBuilder(_fonts);
        builder.BeginText();

        var error = Assert.Throws<ArgumentException>(() => builder.SetFont(other, 12));

        Assert.Contains("another document", error.Message, StringComparison.Ordinal);
    }

    // The TJ form (spec T-13): each extra advance d is written as -d * 1000 / size after its CID, and a zero writes
    // nothing. The spec's own example is the first case.
    [Fact]
    public void ShowGlyphsWithAdvancesWritesTj()
    {
        _font.Encode(1, ' ');
        using var builder = new ContentStreamBuilder(_fonts);

        builder.BeginText().SetFont(_font, 10)
            .ShowGlyphs([1, 2, 3], [0, 0.5, 0])
            .ShowGlyphs([1, 2, 3], [0, 0, 0])
            .ShowGlyphs([1, 2], [-0.25, 0])
            .ShowGlyphs([1, 2], [0, 0.5])
            .EndText();

        Assert.Equal(
            "BT\n/F1 10 Tf\n[<00010002> -50 <0003>] TJ\n[<000100020003>] TJ\n[<0001> 25 <0002>] TJ\n[<00010002> -50] TJ\nET\n",
            Finish(builder));
    }

    [Fact]
    public void ShowGlyphsWithAdvancesRefusals()
    {
        using var builder = new ContentStreamBuilder(_fonts);

        builder.BeginText();
        Assert.Throws<InvalidOperationException>(() => builder.ShowGlyphs([1], [0]));
        builder.SetFont(_font, 12);
        Assert.Throws<ArgumentException>(() => builder.ShowGlyphs([1, 2], [0]));
        Assert.Throws<ArgumentException>(() => builder.ShowGlyphs([1, 0], [0, 0]));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.ShowGlyphs([1, 2], [double.NaN, 0]));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.ShowGlyphs([1, 2], [0, double.PositiveInfinity]));
        builder.SetFont(_font, 0);
        Assert.Throws<InvalidOperationException>(() => builder.ShowGlyphs([1], [0.5]));
        builder.EndText();

        Assert.Equal("BT\n/F1 12 Tf\n/F1 0 Tf\nET\n", Finish(builder));
    }

    public void Dispose() => _file.Dispose();

    private static string Finish(ContentStreamBuilder builder) => Encoding.Latin1.GetString(builder.Finish());
}
