using System.Buffers.Binary;
using Maviray.Pdf.Fonts;

namespace Maviray.Pdf.Tests.Fonts;

public sealed class CompositeGlyphTests
{
    // A composite glyph's data is a 10-byte header, then records of flags (2), glyph ID (2), arguments and transform.
    // The first record's glyph ID is therefore always at offset 12.
    [Fact]
    public void ReadsOneComponentWithByteArguments()
    {
        var (data, glyphCount) = Composite(new TestComponent(2, 0x0000));

        Assert.Equal([new GlyphComponent(2, 12)], CompositeGlyph.ReadComponents(data, glyphCount));
    }

    [Theory]
    [InlineData((ushort)0x0000, 18)] // byte arguments: a 6-byte record
    [InlineData((ushort)0x0001, 20)] // word arguments
    [InlineData((ushort)0x0008, 20)] // one F2Dot14 scale
    [InlineData((ushort)0x0040, 22)] // X and Y scales
    [InlineData((ushort)0x0080, 26)] // a two-by-two matrix
    [InlineData((ushort)0x00C9, 22)] // all four: word arguments, and the single scale wins over the other two
    public void RecordLengthFollowsTheFlags(ushort flags, int secondOffset)
    {
        var (data, glyphCount) = Composite(new TestComponent(2, flags), new TestComponent(3, 0x0000));

        var components = CompositeGlyph.ReadComponents(data, glyphCount);

        Assert.Equal([new GlyphComponent(2, 12), new GlyphComponent(3, secondOffset)], components);
    }

    [Fact]
    public void InstructionsAfterTheLastRecordAreIgnored()
    {
        var (data, glyphCount) = Composite(new TestComponent(2, 0x0000), new TestComponent(3, 0x0100));

        Assert.Equal([new GlyphComponent(2, 12), new GlyphComponent(3, 18)], CompositeGlyph.ReadComponents(data, glyphCount));
    }

    [Theory]
    [InlineData(12)] // inside the first record
    [InlineData(16)] // the first record says more follow, and nothing does
    [InlineData(19)] // inside the second record
    public void RefusesATruncatedRecord(int length)
    {
        var (data, glyphCount) = Composite(new TestComponent(2, 0x0000), new TestComponent(3, 0x0000));

        var error = Assert.Throws<InvalidDataException>(() => CompositeGlyph.ReadComponents(data[..length], glyphCount));

        Assert.Contains("'glyf'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesAComponentPastTheFont()
    {
        var (data, glyphCount) = Composite(new TestComponent(9, 0x0000));

        var error = Assert.Throws<InvalidDataException>(() => CompositeGlyph.ReadComponents(data, glyphCount));

        Assert.Contains("'glyf'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SimpleAndEmptyGlyphsHaveNoComponents()
    {
        var font = TrueTypeFont.Load(new TestFontBuilder().Build());

        Assert.Empty(CompositeGlyph.ReadComponents(font.GetGlyphData(2), font.GlyphCount));
        Assert.Empty(CompositeGlyph.ReadComponents(font.GetGlyphData(1), font.GlyphCount));
    }

    // Real hinted data: in every style, Ä is A plus a mark, and the mark's record carries instructions (0x0107).
    [Theory]
    [MemberData(nameof(TestFonts.Styles), MemberType = typeof(TestFonts))]
    public void NotoSansADieresisIsAPlusAMark(string style)
    {
        var font = TestFonts.NotoSans(style);
        var data = font.GetGlyphData(font.GetGlyphId('Ä'));

        var components = CompositeGlyph.ReadComponents(data, font.GlyphCount);

        Assert.Equal(2, components.Length);
        Assert.Equal(font.GetGlyphId('A'), components[0].GlyphId);
        Assert.Equal(0x0107, BinaryPrimitives.ReadUInt16BigEndian(data[(components[1].GlyphIdOffset - 2)..]));
        foreach (var component in components)
        {
            Assert.Equal(component.GlyphId, BinaryPrimitives.ReadUInt16BigEndian(data[component.GlyphIdOffset..]));
        }
    }

    private static (byte[] Data, int GlyphCount) Composite(params TestComponent[] components)
    {
        var font = TrueTypeFont.Load(new TestFontBuilder().CompositeGlyph(700, components).Build());
        return (font.GetGlyphData(4).ToArray(), font.GlyphCount);
    }
}
