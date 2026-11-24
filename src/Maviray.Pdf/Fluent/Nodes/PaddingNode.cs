using Maviray.Pdf.Elements;
using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Fluent.Nodes;

/// <summary>A <c>Padding</c> call.</summary>
internal sealed class PaddingNode(double left, double top, double right, double bottom) : Node
{
    public ContainerSlot Slot { get; } = new();

    public override Element Build(BuildContext context) => new Padding(left, top, right, bottom, Slot.Build(context));
}
