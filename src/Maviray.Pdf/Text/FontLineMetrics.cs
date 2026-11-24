using Maviray.Pdf.Fonts;

namespace Maviray.Pdf.Text;

/// <summary>
/// A font's ascent, descent and line gap for laying out lines, in font units (spec T-08). They come from the
/// <c>OS/2</c> typographic values when the font sets <c>USE_TYPO_METRICS</c>, and from <c>hhea</c> otherwise. A
/// negative line gap counts as 0, and an extent that isn't positive falls back to 0.8 em above and 0.2 em below.
/// </summary>
internal readonly record struct FontLineMetrics(int Ascent, int Descent, int LineGap, int UnitsPerEm)
{
    /// <summary>The font's normal line height as a multiple of the size: (A − D + G) ÷ unitsPerEm.</summary>
    public double NormalLineHeight => (Ascent - Descent + LineGap) / (double)UnitsPerEm;

    public static FontLineMetrics Of(TrueTypeFont font)
    {
        ArgumentNullException.ThrowIfNull(font);

        var metrics = font.UseTypographicMetrics && font.TypographicMetrics is { } typographic
            ? typographic
            : font.HorizontalHeaderMetrics;
        var ascent = metrics.Ascender;
        var descent = metrics.Descender;
        if (ascent - descent <= 0)
        {
            ascent = font.UnitsPerEm * 4 / 5;
            descent = -font.UnitsPerEm / 5;
        }

        return new FontLineMetrics(ascent, descent, Math.Max(0, metrics.LineGap), font.UnitsPerEm);
    }

    /// <summary>
    /// How far a span at <paramref name="size"/> reaches above and below the baseline, in points. With m the line
    /// height (or the normal one), the leading L = m·s − (A − D)·s ÷ unitsPerEm is shared half above and half below,
    /// and may be negative.
    /// </summary>
    public (double Above, double Below) Extent(double size, double? lineHeight)
    {
        var scale = size / UnitsPerEm;
        var leading = ((lineHeight ?? NormalLineHeight) * size) - ((Ascent - Descent) * scale);
        return ((Ascent * scale) + (leading / 2), (-Descent * scale) + (leading / 2));
    }
}
