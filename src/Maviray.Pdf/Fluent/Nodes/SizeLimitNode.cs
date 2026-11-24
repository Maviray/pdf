using Maviray.Pdf.Elements;
using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Fluent.Nodes;

/// <summary>A size limit call, named in paths by the call.</summary>
internal sealed class SizeLimitNode(string name, double minWidth, double maxWidth, double minHeight, double maxHeight) : Node
{
    public ContainerSlot Slot { get; } = new();

    public override Element Build(BuildContext context) => new SizeLimit(name, minWidth, maxWidth, minHeight, maxHeight, Slot.Build(context));
}
