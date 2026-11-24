using Maviray.Pdf.Elements;
using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Fluent.Nodes;

/// <summary>A <c>PageBreak</c> call.</summary>
internal sealed class PageBreakNode : Node
{
    public override Element Build(BuildContext context) => new PageBreak();
}
