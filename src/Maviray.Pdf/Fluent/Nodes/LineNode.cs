using Maviray.Pdf.Elements;
using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Fluent.Nodes;

/// <summary>A <c>HorizontalLine</c> or <c>VerticalLine</c> call.</summary>
internal sealed class LineNode(bool vertical, double thickness, Color color) : Node
{
    public override Element Build(BuildContext context) =>
        vertical ? new VerticalLine(thickness, color.ToRgb()) : new HorizontalLine(thickness, color.ToRgb());
}
