using System.Buffers.Binary;
using Maviray.Pdf.Fonts;

namespace Maviray.Pdf.Tests.Fonts;

/// <summary>
/// Fonts are untrusted input (spec F-02): whatever the bytes, loading and every lookup either work or throw
/// <see cref="InvalidDataException"/> or <see cref="NotSupportedException"/>, and a font that loads keeps its
/// mapping contract. These tests damage two small built fonts, one per <c>cmap</c> format, in every way one truncation
/// or one changed byte can, which is cheap and deterministic; fuzzing follows in P2.
/// </summary>
public sealed class DamagedFontTests
{
    private static readonly byte[] Format4Font = new TestFontBuilder()
        .CompositeGlyph(700, new TestComponent(2, 0x0001), new TestComponent(3, 0x0008))
        .Build();

    private static readonly byte[] Format12Font = new TestFontBuilder()
        .CmapFormat12()
        .Map(0x1F600, 4)
        .CompositeGlyph(700, new TestComponent(2, 0x00C9), new TestComponent(3, 0x0040), new TestComponent(1, 0x0180))
        .Build();

    public static TheoryData<string> Fonts => new() { "format 4", "format 12" };

    [Theory]
    [MemberData(nameof(Fonts))]
    public void EveryTruncationThrowsInvalidData(string kind)
    {
        var valid = FontOf(kind);
        var end = EndOfLastTable(valid);

        for (var length = 0; length < end; length++)
        {
            var file = valid[..length];
            Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(file));
        }

        for (var length = end; length <= valid.Length; length++)
        {
            Exercise(TrueTypeFont.Load(valid[..length]));
        }
    }

    [Theory]
    [MemberData(nameof(Fonts))]
    public void EveryByteChangeLoadsOrThrowsTheDocumentedExceptions(string kind)
    {
        var valid = FontOf(kind);
        var escapes = new List<string>();
        for (var offset = 0; offset < valid.Length; offset++)
        {
            foreach (var value in new byte[] { 0x00, 0x7F, 0x80, 0xFF })
            {
                var file = (byte[])valid.Clone();
                file[offset] = value;
                TrueTypeFont font;
                try
                {
                    font = TrueTypeFont.Load(file);
                }
                catch (Exception error) when (error is InvalidDataException or NotSupportedException)
                {
                    continue; // documented
                }
#pragma warning disable CA1031 // every other exception is the finding this test reports
                catch (Exception error)
                {
                    escapes.Add($"byte {offset} = 0x{value:X2}, loading: {error.GetType().Name}: {error.Message}");
                    continue;
                }

                try
                {
                    Exercise(font);
                }
                catch (Exception error)
                {
                    escapes.Add($"byte {offset} = 0x{value:X2}, lookups: {error.GetType().Name}: {error.Message}");
                }
#pragma warning restore CA1031
            }
        }

        Assert.True(escapes.Count == 0, $"{escapes.Count} changes escaped. The first: {string.Join(" | ", escapes.Take(5))}");
    }

    /// <summary>
    /// Every lookup on a loaded font. A glyph whose data is damaged may throw <see cref="InvalidDataException"/>; that is
    /// caught per glyph, so it can't hide a later escape. The mappings must be strictly ascending, never glyph 0 or a
    /// glyph past the font, and agree with <see cref="TrueTypeFont.GetGlyphId"/>; a broken promise throws
    /// <see cref="InvalidOperationException"/>, which the sweep reports.
    /// </summary>
    private static void Exercise(TrueTypeFont font)
    {
        for (var codePoint = 0; codePoint <= 0x2FF; codePoint++)
        {
            _ = font.GetGlyphId(codePoint);
        }

        _ = font.GetGlyphId(0x1F600);
        var previous = -1;
        foreach (var (codePoint, glyph) in font.CharacterMappings().Take(100_000))
        {
            if (codePoint <= previous || glyph == 0 || glyph >= font.GlyphCount || font.GetGlyphId(codePoint) != glyph)
            {
                throw new InvalidOperationException(
                    $"mapping U+{codePoint:X4} → {glyph} breaks the contract: after U+{previous:X4}, lookup gives {font.GetGlyphId(codePoint)}, {font.GlyphCount} glyphs");
            }

            previous = codePoint;
        }

        for (var glyph = 0; glyph < font.GlyphCount; glyph++)
        {
            _ = font.GetAdvanceWidth((ushort)glyph);
            try
            {
                _ = font.GetGlyphHeader((ushort)glyph);
                _ = CompositeGlyph.ReadComponents(font.GetGlyphData((ushort)glyph), font.GlyphCount);
            }
            catch (InvalidDataException)
            {
                // documented for a damaged glyph; the next glyph is still checked
            }
        }
    }

    private static byte[] FontOf(string kind) => kind == "format 4" ? Format4Font : Format12Font;

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
