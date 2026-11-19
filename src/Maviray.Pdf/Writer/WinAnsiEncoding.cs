namespace Maviray.Pdf.Writer;

/// <summary>
/// Maps Unicode text to the single-byte WinAnsiEncoding of the standard fonts (spec D-10), using the table in Annex D of
/// ISO 32000-1: printable ASCII and Latin-1 map to themselves, and 27 characters map into <c>0x80</c>–<c>0x9F</c> as in
/// Windows-1252. Our own table is used instead of <c>System.Text.Encoding.CodePages</c>, which needs a process-wide
/// registration. Text isn't normalized in P0, so a decomposed character is refused.
/// </summary>
internal static class WinAnsiEncoding
{
    /// <summary>Gives the WinAnsi code of <paramref name="character"/>, if it has one.</summary>
    public static bool TryEncode(char character, out byte code)
    {
        if (character is (>= ' ' and <= '~') or (>= ' ' and <= 'ÿ'))
        {
            code = (byte)character;
            return true;
        }

        code = character switch
        {
            '€' => 0x80, // €
            '‚' => 0x82, // ‚
            'ƒ' => 0x83, // ƒ
            '„' => 0x84, // „
            '…' => 0x85, // …
            '†' => 0x86, // †
            '‡' => 0x87, // ‡
            'ˆ' => 0x88, // ˆ
            '‰' => 0x89, // ‰
            'Š' => 0x8A, // Š
            '‹' => 0x8B, // ‹
            'Œ' => 0x8C, // Œ
            'Ž' => 0x8E, // Ž
            '‘' => 0x91, // ‘
            '’' => 0x92, // ’
            '“' => 0x93, // “
            '”' => 0x94, // ”
            '•' => 0x95, // •
            '–' => 0x96, // –
            '—' => 0x97, // —
            '˜' => 0x98, // ˜
            '™' => 0x99, // ™
            'š' => 0x9A, // š
            '›' => 0x9B, // ›
            'œ' => 0x9C, // œ
            'ž' => 0x9E, // ž
            'Ÿ' => 0x9F, // Ÿ
            _ => 0,
        };

        return code != 0;
    }

    /// <summary>Encodes <paramref name="text"/>.</summary>
    /// <exception cref="ArgumentException">A character has no WinAnsi code. The message names it and its index.</exception>
    public static byte[] Encode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var bytes = new byte[text.Length];
        for (var index = 0; index < text.Length; index++)
        {
            if (!TryEncode(text[index], out bytes[index]))
            {
                throw new ArgumentException(
                    $"Character U+{(int)text[index]:X4} at index {index} has no code in WinAnsiEncoding, the encoding of the standard fonts.",
                    nameof(text));
            }
        }

        return bytes;
    }
}
