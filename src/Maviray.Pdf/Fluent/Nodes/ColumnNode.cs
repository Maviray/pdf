using Maviray.Pdf.Elements;
using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Fluent.Nodes;

/// <summary>A <c>Column</c> call: its items, and the spacing between them.</summary>
internal sealed class ColumnNode : Node
{
    public List<ContainerSlot> Items { get; } = [];

    public double Spacing { get; set; }

    public override Element Build(BuildContext context) => new Column([.. Items.Select(item => item.Build(context))], Spacing);
}
