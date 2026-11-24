using Maviray.Pdf.Elements;
using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Fluent.Nodes;

/// <summary>A <c>Row</c> call: its items with their kinds, and the spacing between them.</summary>
internal sealed class RowNode : Node
{
    public List<(RowItemKind Kind, double Value, ContainerSlot Slot)> Items { get; } = [];

    public double Spacing { get; set; }

    public override Element Build(BuildContext context) =>
        new Row([.. Items.Select(item => new RowItem(item.Kind, item.Value, item.Slot.Build(context)))], Spacing);
}
