using Maviray.Pdf.Text;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Drawing;

/// <summary>
/// One drawing call the recording canvas saw, at its absolute position on the page: a <c>Rectangle</c> with its size and
/// colour, or a <c>TextLine</c> with its layout, line and span colours.
/// </summary>
internal sealed record CanvasCall(
    string Kind,
    double X,
    double Y,
    double Width = 0,
    double Height = 0,
    RgbColor? Color = null,
    TextLayout? Layout = null,
    int LineIndex = -1,
    IReadOnlyList<RgbColor>? SpanColors = null);
