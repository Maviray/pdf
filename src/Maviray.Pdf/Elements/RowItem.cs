using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Elements;

/// <summary>An item of a Row (spec L-06).</summary>
/// <param name="Kind">How the item gets its width.</param>
/// <param name="Value">The width in points for <see cref="RowItemKind.Fixed"/>, the weight for
/// <see cref="RowItemKind.Flex"/>, and unused for <see cref="RowItemKind.Auto"/>.</param>
/// <param name="Element">The item's content.</param>
internal readonly record struct RowItem(RowItemKind Kind, double Value, Element Element);
