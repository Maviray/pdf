using Maviray.Pdf.Elements;
using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Fluent.Nodes;

/// <summary>A <c>Border</c> call, on all sides or one.</summary>
internal sealed class BorderNode(double left, double top, double right, double bottom, Color color, bool stretch) : Node
{
    public ContainerSlot Slot { get; } = new();

    public override Element Build(BuildContext context) => new Border(left, top, right, bottom, color.ToRgb(), Slot.Build(context), stretch);
}
