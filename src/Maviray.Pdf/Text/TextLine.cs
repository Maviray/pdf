namespace Maviray.Pdf.Text;

/// <summary>
/// One line of a filled paragraph (spec T-14). Its range runs to the next line's start, so it holds its hanging
/// spaces and its break character. Positions are in points from the paragraph's left edge and top, y growing downward.
/// </summary>
internal sealed class TextLine(
    int start,
    int length,
    double width,
    double offset,
    double top,
    double height,
    double baseline,
    IReadOnlyList<GlyphRun> runs,
    IReadOnlyList<TextDecoration> decorations)
{
    /// <summary>The UTF-16 start of the line's range in the prepared text.</summary>
    public int Start { get; } = start;

    /// <summary>The UTF-16 length of the line's range.</summary>
    public int Length { get; } = length;

    /// <summary>The drawn advances and the letter spacing between them, without hanging spaces (T-09).</summary>
    public double Width { get; } = width;

    /// <summary>How far the line sits from the paragraph's left edge, by its alignment (T-10).</summary>
    public double Offset { get; } = offset;

    /// <summary>The line's top, from the paragraph's top.</summary>
    public double Top { get; } = top;

    public double Height { get; } = height;

    /// <summary>The baseline, from the line's top (T-08).</summary>
    public double Baseline { get; } = baseline;

    /// <summary>The line's glyphs, one run per span segment, without hanging spaces.</summary>
    public IReadOnlyList<GlyphRun> Runs { get; } = runs;

    public IReadOnlyList<TextDecoration> Decorations { get; } = decorations;
}
