using Maviray.Pdf.Elements;
using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Fluent.Nodes;

/// <summary>A <c>Background</c> call.</summary>
internal sealed class BackgroundNode(Color color, bool stretch) : Node
{
    public ContainerSlot Slot { get; } = new();

    public override Element Build(BuildContext context) => new Background(color.ToRgb(), Slot.Build(context), stretch);
}
