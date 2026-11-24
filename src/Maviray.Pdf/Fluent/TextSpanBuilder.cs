using Maviray.Pdf.Elements;
using Maviray.Pdf.Fluent.Nodes;

namespace Maviray.Pdf;

/// <summary>
/// Styles one span of text. Each call sets one value of the span's style, over the style it inherits, and returns the
/// builder. A span can only be styled while its document is being composed.
/// </summary>
public sealed class TextSpanBuilder
{
    private readonly TextNode _node;

    internal TextSpanBuilder(TextNode node, TextRunKind kind, string text)
    {
        _node = node;
        Kind = kind;
        Text = text;
    }

    internal TextRunKind Kind { get; }

    internal string Text { get; }

    internal TextStyle Style { get; private set; } = new();

    /// <summary>The span's font family, as registered in the document's font registry.</summary>
    /// <param name="family">The family's name.</param>
    /// <exception cref="ArgumentException"><paramref name="family"/> is blank.</exception>
    public TextSpanBuilder FontFamily(string family) => Set(Style.FontFamily(family));

    /// <summary>The span's font size.</summary>
    /// <param name="size">The size in points, greater than 0 and at most 1,000.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is out of range.</exception>
    public TextSpanBuilder FontSize(double size) => Set(Style.FontSize(size));

    /// <summary>The span's font weight.</summary>
    /// <param name="weight">The weight, from 1 to 1,000.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="weight"/> is out of range.</exception>
    public TextSpanBuilder Weight(FontWeight weight) => Set(Style.Weight(weight));

    /// <summary>The bold weight, 700.</summary>
    public TextSpanBuilder Bold() => Set(Style.Bold());

    /// <summary>Italic, or upright to undo an inherited italic.</summary>
    /// <param name="value">True for italic, false for upright.</param>
    public TextSpanBuilder Italic(bool value = true) => Set(Style.Italic(value));

    /// <summary>The span's colour.</summary>
    /// <param name="color">The colour.</param>
    public TextSpanBuilder FontColor(Color color) => Set(Style.FontColor(color));

    /// <summary>An underline, or none to undo an inherited one.</summary>
    /// <param name="value">True to underline, false for none.</param>
    public TextSpanBuilder Underline(bool value = true) => Set(Style.Underline(value));

    /// <summary>A line through the text, or none to undo an inherited one.</summary>
    /// <param name="value">True to strike through, false for none.</param>
    public TextSpanBuilder Strikethrough(bool value = true) => Set(Style.Strikethrough(value));

    /// <summary>Space after each character but the last of a line, as a fraction of the font size.</summary>
    /// <param name="em">The spacing in em. It may be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="em"/> isn't finite.</exception>
    public TextSpanBuilder LetterSpacing(double em) => Set(Style.LetterSpacing(em));

    /// <summary>The line height, as a multiple of the font size.</summary>
    /// <param name="multiple">The multiple, positive and finite.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="multiple"/> isn't positive and finite.</exception>
    public TextSpanBuilder LineHeight(double multiple) => Set(Style.LineHeight(multiple));

    private TextSpanBuilder Set(TextStyle style)
    {
        _node.ThrowIfBuilt();
        Style = style;
        return this;
    }
}
