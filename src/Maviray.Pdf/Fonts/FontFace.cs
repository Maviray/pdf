namespace Maviray.Pdf.Fonts;

/// <summary>A registered face: its family as registered, its weight from 1 to 1,000, its style, and its font (L-09).</summary>
internal sealed record FontFace(string Family, int Weight, bool Italic, TrueTypeFont Font);
