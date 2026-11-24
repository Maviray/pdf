namespace Maviray.Pdf.Elements;

/// <summary>A span of a Text element (spec L-07): what it shows, and its own style.</summary>
/// <param name="Kind">Its own text, the page number, or the page count.</param>
/// <param name="Text">The text, for <see cref="TextRunKind.Text"/> only.</param>
/// <param name="Style">The span's style, over the element's.</param>
internal readonly record struct TextRun(TextRunKind Kind, string Text, TextStyle Style);
