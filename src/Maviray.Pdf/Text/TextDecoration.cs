namespace Maviray.Pdf.Text;

/// <summary>
/// An underline or strikethrough of one span on one line, as a filled rectangle (spec T-11): x and width from the
/// paragraph's left edge, and its top edge measured down from the line's top, with the stroke extending downward from
/// it by its thickness.
/// </summary>
internal readonly record struct TextDecoration(
    DecorationKind Kind,
    int SpanIndex,
    double X,
    double Width,
    double Top,
    double Thickness);
