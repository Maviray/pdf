using System.Text;

namespace Maviray.Pdf.Fonts;

/// <summary>
/// The PostScript, family and subfamily names from <c>name</c>, which is optional (spec F-03). For each name the best
/// record wins: Windows Unicode in US English, then any Windows Unicode, then the Unicode platform, then Macintosh
/// Roman in English. An empty string counts as absent.
/// </summary>
internal sealed record NamingTable(string? PostScriptName, string? FamilyName, string? SubfamilyName)
{
    private const ushort Family = 1;
    private const ushort Subfamily = 2;
    private const ushort PostScript = 6;
    private const ushort TypographicFamily = 16;
    private const ushort TypographicSubfamily = 17;

    public static NamingTable Empty { get; } = new(null, null, null);

    public static NamingTable Parse(ReadOnlySpan<byte> table)
    {
        var reader = new FontTableReader(table, "name");
        var count = reader.UInt16(2);
        var storage = reader.UInt16(4);
        var best = new Dictionary<ushort, (int Rank, string Text)>();
        for (var i = 0; i < count; i++)
        {
            var record = 6 + (12 * i);
            var platform = reader.UInt16(record);
            var encoding = reader.UInt16(record + 2);
            var language = reader.UInt16(record + 4);
            var nameId = reader.UInt16(record + 6);
            var length = reader.UInt16(record + 8);
            var bytes = reader.Slice(storage + reader.UInt16(record + 10), length);

            var rank = Rank(platform, encoding, language);
            if (rank < 0 || nameId is not (Family or Subfamily or PostScript or TypographicFamily or TypographicSubfamily))
            {
                continue;
            }

            if (best.TryGetValue(nameId, out var current) && current.Rank <= rank)
            {
                continue;
            }

            var text = Decode(reader, platform, bytes);
            if (text.Length > 0)
            {
                best[nameId] = (rank, text);
            }
        }

        string? Name(ushort id) => best.TryGetValue(id, out var entry) ? entry.Text : null;
        return new NamingTable(Name(PostScript), Name(TypographicFamily) ?? Name(Family), Name(TypographicSubfamily) ?? Name(Subfamily));
    }

    // Lower is better; -1 is a record the library doesn't read.
    private static int Rank(ushort platform, ushort encoding, ushort language) => (platform, encoding) switch
    {
        (3, 1 or 10) => language == 0x409 ? 0 : 1,
        (0, _) => 2,
        (1, 0) when language == 0 => 3,
        _ => -1,
    };

    private static string Decode(FontTableReader reader, ushort platform, ReadOnlySpan<byte> bytes)
    {
        if (platform == 1)
        {
            // Macintosh Roman: only its ASCII half is decoded; the rest becomes '?'.
            return string.Create(bytes.Length, bytes.ToArray(), static (chars, source) =>
            {
                for (var i = 0; i < chars.Length; i++)
                {
                    chars[i] = source[i] < 0x80 ? (char)source[i] : '?';
                }
            });
        }

        if (bytes.Length % 2 != 0)
        {
            throw reader.Error($"has a UTF-16 name record of odd length ({bytes.Length} bytes)");
        }

        return Encoding.BigEndianUnicode.GetString(bytes);
    }
}
