namespace Maviray.Pdf.Elements;

/// <summary>
/// A span's style with every value set (spec L-08): the span's own, over the Text element's, over the inherited one
/// (the nearest container default over each outer one, over the page's), over <see cref="BuiltIn"/>.
/// </summary>
/// <param name="Family">The font family.</param>
/// <param name="Size">The size in points.</param>
/// <param name="Weight">The weight, from 1 to 1,000.</param>
/// <param name="Italic">Whether it asks for an italic face.</param>
/// <param name="Color">The text's colour.</param>
/// <param name="Underline">Whether it is underlined.</param>
/// <param name="Strikethrough">Whether it is struck through.</param>
/// <param name="LetterSpacing">The letter spacing in em.</param>
/// <param name="LineHeight">The line height as a multiple of the size, or null for the font's normal one.</param>
internal sealed record ResolvedTextStyle(
    string Family,
    double Size,
    int Weight,
    bool Italic,
    Color Color,
    bool Underline,
    bool Strikethrough,
    double LetterSpacing,
    double? LineHeight)
{
    /// <summary>The built-in default: Noto Sans, 12 pt, weight 400, upright, black, no decoration, no letter spacing,
    /// and the font's normal line height.</summary>
    public static ResolvedTextStyle BuiltIn { get; } = new("Noto Sans", 12, 400, false, Color.Black, false, false, 0, null);

    public static ResolvedTextStyle Resolve(TextStyle span, TextStyle element, TextStyle inherited)
    {
        ArgumentNullException.ThrowIfNull(span);
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(inherited);
        var style = span.Over(element).Over(inherited);
        return new(
            style.Family ?? BuiltIn.Family,
            style.Size ?? BuiltIn.Size,
            style.WeightValue ?? BuiltIn.Weight,
            style.IsItalic ?? BuiltIn.Italic,
            style.Color ?? BuiltIn.Color,
            style.IsUnderlined ?? BuiltIn.Underline,
            style.IsStruckThrough ?? BuiltIn.Strikethrough,
            style.LetterSpacingEm ?? BuiltIn.LetterSpacing,
            style.LineHeightMultiple ?? BuiltIn.LineHeight);
    }
}
