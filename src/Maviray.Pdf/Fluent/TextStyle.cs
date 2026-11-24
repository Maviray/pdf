namespace Maviray.Pdf;

/// <summary>
/// How text looks: its font family, size, weight and style, colour, decorations, letter spacing and line height. A
/// style is immutable: each method returns a new one. A value that isn't set is inherited, from the span to the Text
/// element, to the nearest container's default style, to the page's, and last to the built-in default: Noto Sans,
/// 12 pt, weight 400, upright, black, no decoration, no letter spacing, and the font's normal line height.
/// </summary>
public sealed class TextStyle
{
    private const double MaxSize = 1000;

    /// <summary>Creates a style that sets nothing, so it inherits every value.</summary>
    public TextStyle()
    {
    }

    private TextStyle(TextStyle source)
    {
        Family = source.Family;
        Size = source.Size;
        WeightValue = source.WeightValue;
        IsItalic = source.IsItalic;
        Color = source.Color;
        IsUnderlined = source.IsUnderlined;
        IsStruckThrough = source.IsStruckThrough;
        LetterSpacingEm = source.LetterSpacingEm;
        LineHeightMultiple = source.LineHeightMultiple;
    }

    internal string? Family { get; private init; }

    internal double? Size { get; private init; }

    internal int? WeightValue { get; private init; }

    internal bool? IsItalic { get; private init; }

    internal Color? Color { get; private init; }

    internal bool? IsUnderlined { get; private init; }

    internal bool? IsStruckThrough { get; private init; }

    internal double? LetterSpacingEm { get; private init; }

    internal double? LineHeightMultiple { get; private init; }

    /// <summary>The font family, as registered in the document's font registry.</summary>
    /// <param name="family">The family's name, such as <c>Noto Sans</c>.</param>
    /// <exception cref="ArgumentException"><paramref name="family"/> is blank.</exception>
    public TextStyle FontFamily(string family)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(family);
        return new(this) { Family = family };
    }

    /// <summary>The font size.</summary>
    /// <param name="size">The size in points, greater than 0 and at most 1,000.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is 0 or less, above 1,000, or not a number.</exception>
    public TextStyle FontSize(double size)
    {
        if (!(size > 0 && size <= MaxSize))
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "A font size must be greater than 0 points and at most 1,000.");
        }

        return new(this) { Size = size };
    }

    /// <summary>The font weight, which chooses the family's face (CSS Fonts Level 4).</summary>
    /// <param name="weight">The weight, from 1 to 1,000.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="weight"/> isn't from 1 to 1,000.</exception>
    public TextStyle Weight(FontWeight weight)
    {
        if ((int)weight is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), weight, "A weight must be from 1 to 1,000.");
        }

        return new(this) { WeightValue = (int)weight };
    }

    /// <summary>The bold weight, 700.</summary>
    public TextStyle Bold() => Weight(FontWeight.Bold);

    /// <summary>Italic, or upright to undo an inherited italic.</summary>
    /// <param name="value">True for italic, false for upright.</param>
    public TextStyle Italic(bool value = true) => new(this) { IsItalic = value };

    /// <summary>The text's colour.</summary>
    /// <param name="color">The colour.</param>
    public TextStyle FontColor(Color color) => new(this) { Color = color };

    /// <summary>An underline, or none to undo an inherited one.</summary>
    /// <param name="value">True to underline, false for none.</param>
    public TextStyle Underline(bool value = true) => new(this) { IsUnderlined = value };

    /// <summary>A line through the text, or none to undo an inherited one.</summary>
    /// <param name="value">True to strike through, false for none.</param>
    public TextStyle Strikethrough(bool value = true) => new(this) { IsStruckThrough = value };

    /// <summary>Space added after each character but the last of a line, as a fraction of the font size.</summary>
    /// <param name="em">The spacing in em, such as 0.05. It may be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="em"/> isn't a finite number.</exception>
    public TextStyle LetterSpacing(double em)
    {
        if (!double.IsFinite(em))
        {
            throw new ArgumentOutOfRangeException(nameof(em), em, "Letter spacing must be a finite number of em.");
        }

        return new(this) { LetterSpacingEm = em };
    }

    /// <summary>The line height, as a multiple of the font size.</summary>
    /// <param name="multiple">The multiple, such as 1.2: positive and finite.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="multiple"/> isn't positive and finite.</exception>
    public TextStyle LineHeight(double multiple)
    {
        if (!(double.IsFinite(multiple) && multiple > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(multiple), multiple, "A line height must be a positive, finite multiple of the size.");
        }

        return new(this) { LineHeightMultiple = multiple };
    }

    /// <summary>This style, with every value it doesn't set taken from <paramref name="parent"/>.</summary>
    internal TextStyle Over(TextStyle parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        return new(this)
        {
            Family = Family ?? parent.Family,
            Size = Size ?? parent.Size,
            WeightValue = WeightValue ?? parent.WeightValue,
            IsItalic = IsItalic ?? parent.IsItalic,
            Color = Color ?? parent.Color,
            IsUnderlined = IsUnderlined ?? parent.IsUnderlined,
            IsStruckThrough = IsStruckThrough ?? parent.IsStruckThrough,
            LetterSpacingEm = LetterSpacingEm ?? parent.LetterSpacingEm,
            LineHeightMultiple = LineHeightMultiple ?? parent.LineHeightMultiple,
        };
    }
}
