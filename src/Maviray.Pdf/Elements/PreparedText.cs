using Maviray.Pdf.Fonts;
using Maviray.Pdf.Text;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Elements;

/// <summary>
/// A Text element's preparation (spec L-07): the prepared paragraph, each span's colour, the faces that were
/// substituted, and the page number and count it shows, 0 for one it doesn't.
/// </summary>
internal sealed class PreparedText(
    PreparedParagraph paragraph,
    RgbColor[] colors,
    IReadOnlyList<(ResolvedTextStyle Style, FontFace Face)> substitutions,
    int number,
    int total)
{
    public PreparedParagraph Paragraph { get; } = paragraph;

    public RgbColor[] Colors { get; } = colors;

    public IReadOnlyList<(ResolvedTextStyle Style, FontFace Face)> Substitutions { get; } = substitutions;

    public int Number { get; } = number;

    public int Total { get; } = total;
}
