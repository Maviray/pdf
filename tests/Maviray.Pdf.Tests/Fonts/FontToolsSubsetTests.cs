using Maviray.Pdf.Fonts;
using Maviray.Pdf.Tests.Validation;

namespace Maviray.Pdf.Tests.Fonts;

/// <summary>
/// Checks our subsets with fontTools, which we didn't write (spec F-13): it loads each one with checksum errors
/// raised, decompiles every table, and compares every subset glyph with its original. The subsets cover the gate's
/// text, every glyph of each Noto Sans style in reverse order, and a synthetic font with nested composites and every
/// record length.
/// </summary>
public sealed class FontToolsSubsetTests
{
    private const string GateText =
        "Grüße aus Köln: Äpfel, Öl und Übermaß Iğdır'da ılık bir gün; İstanbul, Şişli Əli Gəncədə yaşayır "
        + "Счёт № 42 от 20 ноября Ґанок, їжак і євро Τιμολόγιο για την Αθήνα";

    public static TheoryData<string, string> Subsets
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var style in new[] { "Regular", "Bold", "Italic", "BoldItalic" })
            {
                data.Add(style, "gate-text");
                data.Add(style, "reversed");
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Subsets))]
    public void SubsetsOfNotoSansPassTheFontToolsCheck(string style, string list)
    {
        FontTools.Python();
        var font = TestFonts.NotoSans(style);
        IReadOnlyList<ushort> glyphs = list == "gate-text"
            ? GateTextGlyphs(font)
            : [0, .. Enumerable.Range(1, font.GlyphCount - 1).Reverse().Select(g => (ushort)g)];

        var subset = FontSubsetter.Build(font, glyphs);
        var path = GeneratedFiles.SaveAs(subset.Data.ToArray(), $"-{style}-{list}.ttf");

        Assert.Equal(subset.OriginalGlyphIds.Count, FontTools.CheckSubset(TestFonts.NotoSansPath(style), path, subset.OriginalGlyphIds));
    }

    [Fact]
    public void SyntheticNestedSubsetPassesTheFontToolsCheck()
    {
        FontTools.Python();
        var font = new TestFontBuilder()
            .CompositeGlyph(700, new TestComponent(2, 0x0003, 100, -50), new TestComponent(3, 0x000A))
            .CompositeGlyph(800, new TestComponent(4, 0x0042), new TestComponent(2, 0x0082), new TestComponent(3, 0x0102))
            .Build();

        var subset = FontSubsetter.Build(TrueTypeFont.Load(font), [0, 5, 3]);
        var original = GeneratedFiles.SaveAs(font, "-original.ttf");
        var path = GeneratedFiles.SaveAs(subset.Data.ToArray(), "-subset.ttf");

        Assert.Equal(5, FontTools.CheckSubset(original, path, subset.OriginalGlyphIds));
    }

    /// <summary>
    /// The CIDs F-07 assigns for the gate's text, one per distinct glyph and character in order of first use, after
    /// <c>.notdef</c>; then the glyph of <c>G</c> again, standing for a second character drawn with the same glyph.
    /// </summary>
    private static ushort[] GateTextGlyphs(TrueTypeFont font)
    {
        var seen = new HashSet<(ushort Glyph, int CodePoint)>();
        var glyphs = new List<ushort> { 0 };
        foreach (var rune in GateText.EnumerateRunes())
        {
            var glyph = font.GetGlyphId(rune.Value);
            if (seen.Add((glyph, rune.Value)))
            {
                glyphs.Add(glyph);
            }
        }

        glyphs.Add(font.GetGlyphId('G'));
        return [.. glyphs];
    }
}
