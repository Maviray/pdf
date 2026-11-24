using System.Globalization;
using System.Text.Json;

namespace Maviray.Pdf.Tests.Validation;

/// <summary>
/// What fontTools reads from a font (spec F-13): header values from <c>head</c>, <c>maxp</c>, <c>hhea</c> and
/// <c>OS/2</c> (with the strikeout and USE_TYPO_METRICS, spec T-12), and the glyph and advance width of every code point in the best <c>cmap</c>, without glyph 0.
/// </summary>
internal sealed record FontDump(
    int UnitsPerEm,
    int NumGlyphs,
    int Ascender,
    int Descender,
    int FsType,
    int StrikeoutSize,
    int StrikeoutPosition,
    bool UseTypoMetrics,
    IReadOnlyDictionary<int, (int GlyphId, int Advance)> Cmap)
{
    public static FontDump Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var cmap = new Dictionary<int, (int GlyphId, int Advance)>();
        foreach (var entry in root.GetProperty("cmap").EnumerateObject())
        {
            var pair = entry.Value;
            cmap[int.Parse(entry.Name, CultureInfo.InvariantCulture)] = (pair[0].GetInt32(), pair[1].GetInt32());
        }

        return new FontDump(
            root.GetProperty("unitsPerEm").GetInt32(),
            root.GetProperty("numGlyphs").GetInt32(),
            root.GetProperty("ascender").GetInt32(),
            root.GetProperty("descender").GetInt32(),
            root.GetProperty("fsType").GetInt32(),
            root.GetProperty("strikeoutSize").GetInt32(),
            root.GetProperty("strikeoutPosition").GetInt32(),
            root.GetProperty("useTypoMetrics").GetBoolean(),
            cmap);
    }
}
