namespace Maviray.Pdf.Fonts;

/// <summary>A bounding box in font units.</summary>
internal readonly record struct GlyphBounds(short XMin, short YMin, short XMax, short YMax);
