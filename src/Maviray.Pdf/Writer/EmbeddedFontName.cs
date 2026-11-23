using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Maviray.Pdf.Fonts;

namespace Maviray.Pdf.Writer;

/// <summary>
/// The <c>/BaseFont</c> and <c>/FontName</c> of an embedded font (spec F-09). The base name is the PostScript name,
/// cleaned so a PDF name holds it in every viewer. A subset adds a six-letter tag and <c>+</c> in front. The tag comes
/// from the SHA-256 hash of the base name and the CIDs' glyphs, so it changes only when the subset does.
/// </summary>
internal static class EmbeddedFontName
{
    private const int MaxLength = 63;
    private const string Delimiters = "[](){}<>/%";

    /// <summary>
    /// The PostScript name, or the family and subfamily joined with <c>-</c>, keeping only the characters 0x21 to 0x7E
    /// other than delimiters and <c>%</c>, cut to 63 characters; <c>Font</c> if nothing is left.
    /// </summary>
    public static string BaseName(TrueTypeFont font)
    {
        ArgumentNullException.ThrowIfNull(font);
        var source = font.PostScriptName
            ?? (font.FamilyName is { } family ? font.SubfamilyName is { } subfamily ? $"{family}-{subfamily}" : family : string.Empty);
        var kept = new StringBuilder(source.Length);
        foreach (var c in source)
        {
            if (c is >= '!' and <= '~' && !Delimiters.Contains(c, StringComparison.Ordinal) && kept.Length < MaxLength)
            {
                kept.Append(c);
            }
        }

        return kept.Length == 0 ? "Font" : kept.ToString();
    }

    /// <summary>The subset's name: <c>TAG+BaseName</c>.</summary>
    /// <param name="font">The font the subset comes from.</param>
    /// <param name="glyphs">The original glyph of each CID, from CID 0, which the hash leaves out.</param>
    public static string Tagged(TrueTypeFont font, IReadOnlyList<ushort> glyphs)
    {
        ArgumentNullException.ThrowIfNull(glyphs);
        var name = BaseName(font);
        var input = new byte[name.Length + (2 * Math.Max(0, glyphs.Count - 1))];
        Encoding.ASCII.GetBytes(name, input);
        for (var cid = 1; cid < glyphs.Count; cid++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(input.AsSpan(name.Length + (2 * (cid - 1))), glyphs[cid]);
        }

        var hash = SHA256.HashData(input);
        Span<char> tag = stackalloc char[6];
        for (var i = 0; i < tag.Length; i++)
        {
            tag[i] = (char)('A' + (hash[i] % 26));
        }

        return $"{tag}+{name}";
    }
}
