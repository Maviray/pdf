using System.Buffers.Binary;
using System.Text;
using Maviray.Pdf.Fonts;

namespace Maviray.Pdf.Tests.Fonts;

public sealed class TableDirectoryTests
{
    [Fact]
    public void ReadsTheTablesOfABuiltFont()
    {
        var file = new TestFontBuilder().Build();

        var directory = TableDirectory.Read(file);

        Assert.Equal(["OS/2", "cmap", "glyf", "head", "hhea", "hmtx", "loca", "maxp", "name", "post"], directory.Tags);
        Assert.Equal(0x00010000u, directory.SfntVersion);
        Assert.True(directory.HasTable("glyf"));
        Assert.False(directory.HasTable("fvar"));
        Assert.Equal(54, directory.GetTable(file, "head").Length);
        Assert.True(directory.GetTable(file, "fvar").IsEmpty);
    }

    [Fact]
    public void RefusesACollection()
    {
        var file = new TestFontBuilder().SfntVersion(0x74746366).Build(); // 'ttcf'

        var error = Assert.Throws<NotSupportedException>(() => TableDirectory.Read(file));

        Assert.Contains("collection", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0x774F4646u)] // 'wOFF'
    [InlineData(0x774F4632u)] // 'wOF2'
    public void RefusesWebFonts(uint version)
    {
        var file = new TestFontBuilder().SfntVersion(version).Build();

        var error = Assert.Throws<NotSupportedException>(() => TableDirectory.Read(file));

        Assert.Contains("web font", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesAnUnknownVersion()
    {
        var file = new TestFontBuilder().SfntVersion(0x12345678).Build();

        Assert.Throws<InvalidDataException>(() => TableDirectory.Read(file));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(11)]
    public void RefusesAFileShorterThanAHeader(int length)
    {
        var file = new TestFontBuilder().Build()[..length];

        Assert.Throws<InvalidDataException>(() => TableDirectory.Read(file));
    }

    [Fact]
    public void RefusesRecordsPastTheEnd()
    {
        var file = new TestFontBuilder().Build();
        BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(4), 200);

        Assert.Throws<InvalidDataException>(() => TableDirectory.Read(file));
    }

    [Theory]
    [InlineData(8)] // the record's offset field
    [InlineData(12)] // the record's length field
    public void RefusesATableOutsideTheFile(int field)
    {
        var file = new TestFontBuilder().Build();
        BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(RecordOf(file, "glyf") + field), 0xFFFFFFF0);

        var error = Assert.Throws<InvalidDataException>(() => TableDirectory.Read(file));

        Assert.Contains("'glyf'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesATagListedTwice()
    {
        var file = new TestFontBuilder().Build();
        file.AsSpan(12, 4).CopyTo(file.AsSpan(28, 4)); // the second record takes the first record's tag

        Assert.Throws<InvalidDataException>(() => TableDirectory.Read(file));
    }

    /// <summary>The position of <paramref name="tag"/>'s 16-byte record in the table directory.</summary>
    internal static int RecordOf(byte[] file, string tag)
    {
        var count = BinaryPrimitives.ReadUInt16BigEndian(file.AsSpan(4));
        for (var i = 0; i < count; i++)
        {
            var record = 12 + (16 * i);
            if (Encoding.Latin1.GetString(file, record, 4) == tag)
            {
                return record;
            }
        }

        throw new InvalidOperationException($"The test font has no '{tag}' record.");
    }
}
