using Maviray.Pdf.Fonts;

namespace Maviray.Pdf.Tests.Fonts;

public sealed class EmbeddingPermissionsTests
{
    // Bits 1 to 3: the least restrictive wins (OpenType, OS/2 fsType). Bit 8: no subsetting. Bit 9: bitmaps only.
    [Theory]
    [InlineData((ushort)0x0000, "Subset")]
    [InlineData((ushort)0x0004, "Subset")]
    [InlineData((ushort)0x0008, "Subset")]
    [InlineData((ushort)0x0006, "Subset")]
    [InlineData((ushort)0x000A, "Subset")]
    [InlineData((ushort)0x0100, "Whole")]
    [InlineData((ushort)0x0108, "Whole")]
    [InlineData((ushort)0x0002, "Restricted License")]
    [InlineData((ushort)0x0102, "Restricted License")]
    [InlineData((ushort)0x0200, "Bitmap embedding only")]
    [InlineData((ushort)0x0204, "Bitmap embedding only")]
    [InlineData((ushort)0x0208, "Bitmap embedding only")]
    public void Resolves(ushort fsType, string expected)
    {
        if (expected is "Subset" or "Whole")
        {
            Assert.Equal(Enum.Parse<FontEmbedding>(expected), EmbeddingPermissions.Resolve(fsType));
            return;
        }

        var error = Assert.Throws<NotSupportedException>(() => EmbeddingPermissions.Resolve(fsType));
        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
    }
}
