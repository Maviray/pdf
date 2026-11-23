using Maviray.Pdf.Fonts;
using Maviray.Pdf.Tests.Fonts;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

/// <summary>
/// CIDs (spec F-07): one per glyph and character, assigned from 1 on first use, the same in every page of the
/// document. A character the font lacks is glyph 0 with its own CID, so it still copies out. CIDs end at 65,534.
/// </summary>
public sealed class EmbeddedFontTests : IDisposable
{
    private readonly PdfFileWriter _file = new(new MemoryStream(), new PdfWriterOptions());

    [Fact]
    public void CidsAreAssignedInOrderOfFirstUse()
    {
        var font = Embed(new TestFontBuilder());

        Assert.Equal(1, font.Encode(2, 'A'));
        Assert.Equal(2, font.Encode(3, 'B'));
        Assert.Equal(1, font.Encode(2, 'A'));
        Assert.Equal(3, font.Encode(1, ' '));
        Assert.Equal(4, font.Encode(1, 0xA0)); // one glyph for two characters: two CIDs
        Assert.False(font.IsAssigned(0));
        Assert.All(Enumerable.Range(1, 4), cid => Assert.True(font.IsAssigned((ushort)cid)));
        Assert.False(font.IsAssigned(5));
    }

    [Fact]
    public void ACharacterTheFontLacksGetsItsOwnCid()
    {
        var font = Embed(new TestFontBuilder());

        Assert.Equal(1, font.Encode(0, 0x4E2D));
        Assert.Equal(2, font.Encode(0, 0x4E2E));
    }

    [Theory]
    [InlineData((ushort)4, 'A')]
    [InlineData((ushort)2, -1)]
    [InlineData((ushort)2, 0xD800)]
    [InlineData((ushort)2, 0x110000)]
    public void EncodeRefusesBadArguments(ushort glyph, int codePoint)
    {
        var font = Embed(new TestFontBuilder());

        Assert.Throws<ArgumentOutOfRangeException>(() => font.Encode(glyph, codePoint));
        Assert.Equal(1, font.Encode(2, 'A'));
    }

    [Fact]
    public void TheLastCidIs65534()
    {
        var font = Embed(new TestFontBuilder());

        for (var i = 0; i < 65_534; i++)
        {
            Assert.Equal(i + 1, font.Encode(2, 0x10000 + i));
        }

        var error = Assert.Throws<InvalidOperationException>(() => font.Encode(3, 'B'));
        Assert.Contains("65,534", error.Message, StringComparison.Ordinal);
        Assert.Equal(7, font.Encode(2, 0x10006));
    }

    [Fact]
    public void EmbedReturnsOneFontPerTrueTypeFont()
    {
        var font = TrueTypeFont.Load(new TestFontBuilder().Build());
        var resources = new FontResources(_file);
        using var otherFile = new PdfFileWriter(new MemoryStream(), new PdfWriterOptions());

        Assert.Same(resources.Embed(font), resources.Embed(font));
        Assert.NotSame(resources.Embed(font), new FontResources(otherFile).Embed(font));
        Assert.NotSame(resources.Embed(font), resources.Embed(TrueTypeFont.Load(new TestFontBuilder().Build())));
    }

    public void Dispose() => _file.Dispose();

    private EmbeddedFont Embed(TestFontBuilder builder) => new FontResources(_file).Embed(TrueTypeFont.Load(builder.Build()));
}
