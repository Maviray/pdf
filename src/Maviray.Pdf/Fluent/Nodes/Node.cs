using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Fluent.Nodes;

/// <summary>
/// What a builder call recorded. Nodes are built into elements only after the compose callback returns, so a Text
/// element is made after its span calls finish and each element gets its merged default style (P1.12 plan).
/// </summary>
internal abstract class Node
{
    public abstract Element Build(BuildContext context);
}
