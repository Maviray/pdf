namespace Maviray.Pdf.Fonts;

/// <summary>One set of a font's vertical metrics, in font units. The descender is negative below the baseline.</summary>
internal readonly record struct LineMetrics(int Ascender, int Descender, int LineGap);
