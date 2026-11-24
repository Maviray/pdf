namespace Maviray.Pdf.Text;

/// <summary>A prepared paragraph filled at one width (spec T-14). It is immutable.</summary>
internal sealed class TextLayout(PreparedParagraph source, double width, double height, IReadOnlyList<TextLine> lines)
{
    /// <summary>The paragraph this is a layout of, whose text the lines' ranges refer to.</summary>
    public PreparedParagraph Source { get; } = source;

    /// <summary>The widest line's width. Filling again at this width gives the same lines, when no line overflowed.</summary>
    public double Width { get; } = width;

    /// <summary>The lines' heights, summed.</summary>
    public double Height { get; } = height;

    public IReadOnlyList<TextLine> Lines { get; } = lines;
}
