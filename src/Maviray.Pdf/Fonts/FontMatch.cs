namespace Maviray.Pdf.Fonts;

/// <summary>The face a request matched, and whether its style or weight differs from the request (L-09).</summary>
internal sealed record FontMatch(FontFace Face, bool Substituted);
