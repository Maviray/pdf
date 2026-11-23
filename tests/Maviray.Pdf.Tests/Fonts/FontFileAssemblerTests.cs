using Maviray.Pdf.Fonts;

namespace Maviray.Pdf.Tests.Fonts;

/// <summary>
/// The sfnt layout of the OpenType specification's "Organization of an OpenType font" (spec F-08 step 6). The
/// expected checksums and search fields are worked out by hand from its formulas.
/// </summary>
public sealed class FontFileAssemblerTests
{
    public static TheoryData<byte[], uint> Checksums => new()
    {
        { [], 0u },
        { [0x01, 0x02, 0x03, 0x04], 0x01020304u },
        { [0x01, 0x02, 0x03, 0x04, 0x05], 0x06020304u },
        { [0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x02], 0x00000001u },
    };

    [Theory]
    [MemberData(nameof(Checksums))]
    public void ChecksumSumsBigEndianWords(byte[] data, uint expected)
    {
        Assert.Equal(expected, FontFileAssembler.Checksum(data));
    }

    [Theory]
    [InlineData(1, 16, 0, 0)]
    [InlineData(7, 64, 2, 48)]
    [InlineData(8, 128, 3, 0)]
    [InlineData(11, 128, 3, 48)]
    [InlineData(16, 256, 4, 0)]
    public void SearchFieldsFollowTheTableCount(int count, int searchRange, int entrySelector, int rangeShift)
    {
        var tables = Enumerable.Range(0, count).ToDictionary(i => $"t{i:D3}", _ => new byte[] { 1, 2, 3, 4 }, StringComparer.Ordinal);

        var file = new TestFontFile(FontFileAssembler.Assemble(0x00010000, tables));

        Assert.Equal(((ushort)searchRange, (ushort)entrySelector, (ushort)rangeShift), file.SearchFields);
        Assert.Equal(count, file.Tags.Count);
    }

    [Fact]
    public void TablesAreSortedAlignedAndPadded()
    {
        var tables = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["post"] = [1, 2, 3, 4, 5],
            ["OS/2"] = [6, 7, 8],
            ["cvt "] = [9, 10, 11, 12],
            ["glyf"] = [13],
        };

        var bytes = FontFileAssembler.Assemble(0x00010000, tables);
        var file = new TestFontFile(bytes);

        Assert.Equal(0x00010000u, file.SfntVersion);
        Assert.Equal(["OS/2", "cvt ", "glyf", "post"], file.Tags);
        Assert.Equal(76, file.OffsetOf("OS/2"));
        var end = 76;
        foreach (var tag in file.Tags)
        {
            var offset = file.OffsetOf(tag);
            Assert.Equal(0, offset % 4);
            Assert.All(bytes[end..offset], b => Assert.Equal(0, b));
            Assert.Equal(tables[tag], file.Table(tag));
            Assert.Equal(FontFileAssembler.Checksum(tables[tag]), file.RecordChecksum(tag));
            end = offset + tables[tag].Length;
        }

        Assert.Equal((end + 3) & ~3, bytes.Length);
        Assert.All(bytes[end..], b => Assert.Equal(0, b));
    }

    [Fact]
    public void HeadCarriesTheChecksumAdjustment()
    {
        var head = Enumerable.Range(0, 54).Select(i => (byte)(i % 251)).ToArray();
        byte[] marker = [0xDE, 0xAD, 0xBE, 0xEF];
        marker.CopyTo(head, 8);
        var tables = new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["head"] = head, ["glyf"] = [1, 2, 3, 4, 5, 6] };

        var bytes = FontFileAssembler.Assemble(0x00010000, tables);

        var zeroed = (byte[])head.Clone();
        Array.Clear(zeroed, 8, 4);
        Assert.Equal(0xB1B0AFBAu, FontFileAssembler.Checksum(bytes));
        Assert.Equal(FontFileAssembler.Checksum(zeroed), new TestFontFile(bytes).RecordChecksum("head"));
        Assert.Equal(marker, head[8..12]);
    }

    [Fact]
    public void ReadsBackWithTheTableDirectory()
    {
        var tables = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["maxp"] = [0, 1, 0, 0, 0, 4],
            ["glyf"] = [1, 2, 3],
            ["cvt "] = [4, 5],
        };

        var bytes = FontFileAssembler.Assemble(0x00010000, tables);
        var directory = TableDirectory.Read(bytes);

        Assert.Equal(["cvt ", "glyf", "maxp"], directory.Tags);
        foreach (var (tag, data) in tables)
        {
            Assert.Equal(data, directory.GetTable(bytes, tag).ToArray());
        }
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("abcde")]
    [InlineData("ab\u0001c")]
    [InlineData(null)]
    public void RefusesBadTags(string? tag)
    {
        var tables = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        if (tag is not null)
        {
            tables[tag] = [1];
        }

        Assert.Throws<ArgumentException>(() => FontFileAssembler.Assemble(0x00010000, tables));
    }
}
