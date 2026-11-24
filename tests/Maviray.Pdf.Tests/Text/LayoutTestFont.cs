using Maviray.Pdf.Fonts;
using Maviray.Pdf.Tests.Fonts;
using Maviray.Pdf.Text;

namespace Maviray.Pdf.Tests.Text;

/// <summary>
/// The font of the layout tests (P1.7 plan, Data), with round numbers so expected widths can be worked out by hand.
/// At 1000 units per em and size 10: a letter is 5 pt, a space 2.5 pt, <c>W</c> 9 pt, and a line 10 pt.
/// </summary>
internal static class LayoutTestFont
{
    public const ushort Space = 1;
    public const ushort Letter = 2;
    public const ushort HyphenMinus = 3;
    public const ushort Hyphen = 4;
    public const ushort CombiningAcute = 5;
    public const ushort WideW = 6;

    private static readonly Lazy<TrueTypeFont> Default = new(() => Create());

    /// <summary>The font, built once and shared; a loaded font is immutable.</summary>
    public static TrueTypeFont Font => Default.Value;

    /// <summary>
    /// A new font, after <paramref name="change"/> has adjusted the builder. With <paramref name="mapHyphen"/> false, U+2010
    /// isn't mapped, so a soft hyphen falls back to U+002D.
    /// </summary>
    public static TrueTypeFont Create(Action<TestFontBuilder>? change = null, bool mapHyphen = true)
    {
        var builder = new TestFontBuilder()
            .ClearGlyphs()
            .SimpleGlyph(500, 50, 0, 450, 700) // 0 .notdef
            .EmptyGlyph(250) // 1 space
            .SimpleGlyph(500, 0, 0, 500, 700) // 2 letter
            .SimpleGlyph(300, 0, 300, 300, 400) // 3 hyphen-minus
            .SimpleGlyph(320, 0, 300, 320, 400) // 4 hyphen
            .SimpleGlyph(0, -300, 700, -100, 800) // 5 combining acute
            .SimpleGlyph(900, 0, 0, 900, 700) // 6 W
            .Strikeout(50, 300)
            .Map(0x0020, Space)
            .Map(0x002D, HyphenMinus)
            .Map(0x0301, CombiningAcute)
            .Map('W', WideW);

        if (mapHyphen)
        {
            builder.Map(0x2010, Hyphen);
        }

        foreach (var c in "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVXYZ0123456789.,")
        {
            builder.Map(c, Letter);
        }

        change?.Invoke(builder);
        return TrueTypeFont.Load(builder.Build());
    }

    /// <summary>A style over the shared font, or over <paramref name="font"/>.</summary>
    public static TextStyle Style(
        double size = 10,
        double? lineHeight = null,
        double letterSpacing = 0,
        bool underline = false,
        bool strikethrough = false,
        TrueTypeFont? font = null) =>
        new(font ?? Font, size, lineHeight, letterSpacing, underline, strikethrough);
}
