using System.Buffers.Binary;
using Maviray.Pdf.Fonts;

namespace Maviray.Pdf.Tests.Fonts;

/// <summary>
/// Fonts are untrusted input (spec F-02): whatever the bytes, loading and every lookup either work or throw
/// <see cref="InvalidDataException"/> or <see cref="NotSupportedException"/>. These tests damage a small built font in
/// every way one truncation or one changed byte can, which is cheap and deterministic; fuzzing follows in P2.
/// </summary>
public sealed class DamagedFontTests
{
    private static readonly byte[] Valid = new TestFontBuilder()
        .CompositeGlyph(700, new TestComponent(2, 0x0001), new TestComponent(3, 0x0008))
        .Build();

    [Fact]
    public void EveryTruncationThrowsInvalidData()
    {
        var end = EndOfLastTable(Valid);

        for (var length = 0; length < end; length++)
        {
            var file = Valid[..length];
            Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(file));
        }

        for (var length = end; length <= Valid.Length; length++)
        {
            Exercise(TrueTypeFont.Load(Valid[..length]));
        }
    }

    [Fact]
    public void EveryByteChangeLoadsOrThrowsTheDocumentedExceptions()
    {
        var escapes = new List<string>();
        for (var offset = 0; offset < Valid.Length; offset++)
        {
            foreach (var value in new byte[] { 0x00, 0x7F, 0x80, 0xFF })
            {
                var file = (byte[])Valid.Clone();
                file[offset] = value;
                try
                {
                    Exercise(TrueTypeFont.Load(file));
                }
                catch (Exception error) when (error is InvalidDataException or NotSupportedException)
                {
                    // documented
                }
#pragma warning disable CA1031 // every other exception is the finding this test reports
                catch (Exception error)
#pragma warning restore CA1031
                {
                    escapes.Add($"byte {offset} = 0x{value:X2}: {error.GetType().Name}: {error.Message}");
                }
            }
        }

        Assert.True(escapes.Count == 0, $"{escapes.Count} changes escaped. The first: {string.Join(" | ", escapes.Take(5))}");
    }

    private static void Exercise(TrueTypeFont font)
    {
        for (var codePoint = 0; codePoint <= 0x2FF; codePoint++)
        {
            _ = font.GetGlyphId(codePoint);
        }

        _ = font.GetGlyphId(0x1F600);
        _ = font.CharacterMappings().Take(100_000).Count();
        for (var glyph = 0; glyph < font.GlyphCount; glyph++)
        {
            _ = font.GetAdvanceWidth((ushort)glyph);
            _ = font.GetGlyphHeader((ushort)glyph);
            _ = CompositeGlyph.ReadComponents(font.GetGlyphData((ushort)glyph), font.GlyphCount);
        }
    }

    private static int EndOfLastTable(byte[] file)
    {
        var count = BinaryPrimitives.ReadUInt16BigEndian(file.AsSpan(4));
        var end = 0;
        for (var i = 0; i < count; i++)
        {
            var record = file.AsSpan(12 + (16 * i));
            end = Math.Max(end, (int)(BinaryPrimitives.ReadUInt32BigEndian(record[8..]) + BinaryPrimitives.ReadUInt32BigEndian(record[12..])));
        }

        return end;
    }
}
