using Maviray.Pdf.Elements;
using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Fluent.Nodes;

/// <summary>A <c>DefaultTextStyle</c> call: its style sits over the inherited one for everything inside.</summary>
internal sealed class DefaultStyleNode(TextStyle style) : Node
{
    public ContainerSlot Slot { get; } = new();

    public override Element Build(BuildContext context) => Slot.Build(context with { Inherited = style.Over(context.Inherited) });
}
