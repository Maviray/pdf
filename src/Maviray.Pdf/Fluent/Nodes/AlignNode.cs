using Maviray.Pdf.Elements;
using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Fluent.Nodes;

/// <summary>
/// An alignment call. Chained calls merge into one Align, with the inner call winning on an axis both set, so
/// <c>.AlignRight().AlignBottom()</c> places the child at the bottom right (P1.12 plan, ruling 4).
/// </summary>
internal sealed class AlignNode(HorizontalAlignment? horizontal, VerticalAlignment? vertical) : Node
{
    public ContainerSlot Slot { get; } = new();

    public HorizontalAlignment? Horizontal { get; } = horizontal;

    public VerticalAlignment? Vertical { get; } = vertical;

    public override Element Build(BuildContext context)
    {
        var horizontalAxis = Horizontal;
        var verticalAxis = Vertical;
        var slot = Slot;
        while (slot.Child is AlignNode inner)
        {
            horizontalAxis = inner.Horizontal ?? horizontalAxis;
            verticalAxis = inner.Vertical ?? verticalAxis;
            slot = inner.Slot;
        }

        return new Align(horizontalAxis, verticalAxis, slot.Build(context));
    }
}
