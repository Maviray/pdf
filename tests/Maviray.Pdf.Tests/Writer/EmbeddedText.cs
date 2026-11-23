using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

/// <summary>
/// Encodes a string for an embedded font the way F-07 assigns CIDs: one per character, with the glyph the font's
/// <c>cmap</c> gives it, or <c>.notdef</c> when it has none. The text spec replaces this with shaping and measuring.
/// </summary>
internal static class EmbeddedText
{
    public static ushort[] Encode(EmbeddedFont font, string text) =>
        text.EnumerateRunes().Select(rune => font.Encode(font.Font.GetGlyphId(rune.Value), rune.Value)).ToArray();
}
