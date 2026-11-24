using Maviray.Pdf.Text;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Drawing;

/// <summary>
/// What elements draw on (spec L-10), in points, with the origin at the page's top left and y growing downward.
/// </summary>
internal interface ICanvas
{
    /// <summary>Starts a page of <paramref name="width"/> × <paramref name="height"/> points.</summary>
    void BeginPage(double width, double height);

    /// <summary>Ends the page, which then goes to the output. No offset may be pushed.</summary>
    void EndPage();

    /// <summary>Moves the origin by (<paramref name="x"/>, <paramref name="y"/>) until the matching <see cref="PopOffset"/>.
    /// Offsets add up.</summary>
    void PushOffset(double x, double y);

    /// <summary>Undoes the last <see cref="PushOffset"/>.</summary>
    void PopOffset();

    /// <summary>Fills a rectangle whose top-left corner is at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    void FillRectangle(double x, double y, double width, double height, RgbColor color);

    /// <summary>
    /// Draws line <paramref name="lineIndex"/> of <paramref name="layout"/>, with the paragraph's left edge at
    /// <paramref name="x"/> and the line's top at <paramref name="y"/>: each glyph run on the line's baseline, and each
    /// decoration as a filled rectangle, in its span's colour from <paramref name="spanColors"/>.
    /// </summary>
    void DrawTextLine(TextLayout layout, int lineIndex, double x, double y, IReadOnlyList<RgbColor> spanColors);
}
