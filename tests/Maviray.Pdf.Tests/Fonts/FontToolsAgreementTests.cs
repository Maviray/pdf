using Maviray.Pdf.Tests.Validation;

namespace Maviray.Pdf.Tests.Fonts;

/// <summary>
/// Checks our parser against fontTools, which we didn't write (spec F-13): on every Noto Sans style, both read the same
/// header values, and the same glyph and advance width for every code point in the font's best <c>cmap</c>.
/// </summary>
public sealed class FontToolsAgreementTests
{
    [Theory]
    [MemberData(nameof(TestFonts.Styles), MemberType = typeof(TestFonts))]
    public void OurParserAgreesWithFontTools(string style)
    {
        var theirs = FontTools.Dump(TestFonts.NotoSansPath(style));
        var font = TestFonts.NotoSans(style);

        Assert.Equal(theirs.UnitsPerEm, font.UnitsPerEm);
        Assert.Equal(theirs.NumGlyphs, font.GlyphCount);
        Assert.Equal(theirs.Ascender, font.HorizontalHeaderMetrics.Ascender);
        Assert.Equal(theirs.Descender, font.HorizontalHeaderMetrics.Descender);
        Assert.Equal(theirs.FsType, font.FsType);
        Assert.Equal(theirs.StrikeoutSize, font.StrikeoutSize);
        Assert.Equal(theirs.StrikeoutPosition, font.StrikeoutPosition);
        Assert.Equal(theirs.UseTypoMetrics, font.UseTypographicMetrics);

        var ours = font.CharacterMappings().ToDictionary(m => m.CodePoint, m => ((int)m.GlyphId, font.GetAdvanceWidth(m.GlyphId)));
        var differences = new List<string>();
        foreach (var (codePoint, expected) in theirs.Cmap)
        {
            if (!ours.TryGetValue(codePoint, out var actual))
            {
                differences.Add($"U+{codePoint:X4}: fontTools maps it to {expected}, we don't map it");
            }
            else if (actual != expected)
            {
                differences.Add($"U+{codePoint:X4}: fontTools gives {expected}, we give {actual}");
            }
        }

        differences.AddRange(ours.Keys.Where(c => !theirs.Cmap.ContainsKey(c)).Select(c => $"U+{c:X4}: we map it, fontTools doesn't"));
        Assert.True(differences.Count == 0, $"{differences.Count} differences. The first: {string.Join("; ", differences.Take(10))}");
        Assert.True(ours.Count > 2000, $"Only {ours.Count} code points are mapped.");
    }
}
