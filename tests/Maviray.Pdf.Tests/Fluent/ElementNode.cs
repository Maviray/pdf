using Maviray.Pdf.Fluent.Nodes;
using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Tests.Fluent;

/// <summary>A test node that builds a given element, so the container calls can wrap the test elements of P1.9.</summary>
internal sealed class ElementNode(Element element) : Node
{
    public override Element Build(BuildContext context) => element;
}
