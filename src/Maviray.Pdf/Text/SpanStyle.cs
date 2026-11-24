using Maviray.Pdf.Fonts;

namespace Maviray.Pdf.Text;

/// <summary>
/// The values of a span that decide its geometry (spec T-14). Colour isn't one of them: a layout refers to spans by
/// index, and the drawing side looks up their colour. A style is immutable.
/// </summary>
internal sealed class SpanStyle
{
    /// <param name="font">The font, which gives every glyph of the span.</param>
    /// <param name="size">The font size in points, positive and finite.</param>
    /// <param name="lineHeight">The line height as a multiple of the size, positive and finite, or null for the font's
    /// normal line height (T-08).</param>
    /// <param name="letterSpacing">Points added after each drawn grapheme cluster but the last of a line; may be
    /// negative (T-10).</param>
    /// <param name="underline">Whether the span is underlined (T-11).</param>
    /// <param name="strikethrough">Whether the span is struck through (T-11).</param>
    public SpanStyle(
        TrueTypeFont font,
        double size,
        double? lineHeight = null,
        double letterSpacing = 0,
        bool underline = false,
        bool strikethrough = false)
    {
        ArgumentNullException.ThrowIfNull(font);
        if (!double.IsFinite(size) || size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "The size must be a positive, finite number of points.");
        }

        if (lineHeight is { } height && (!double.IsFinite(height) || height <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(lineHeight), height, "The line height must be a positive, finite multiple of the size.");
        }

        if (!double.IsFinite(letterSpacing))
        {
            throw new ArgumentOutOfRangeException(nameof(letterSpacing), letterSpacing, "The letter spacing must be a finite number of points.");
        }

        Font = font;
        Size = size;
        LineHeight = lineHeight;
        LetterSpacing = letterSpacing;
        Underline = underline;
        Strikethrough = strikethrough;
    }

    public TrueTypeFont Font { get; }

    public double Size { get; }

    public double? LineHeight { get; }

    public double LetterSpacing { get; }

    public bool Underline { get; }

    public bool Strikethrough { get; }
}
