namespace Maviray.Pdf.Elements;

/// <summary>How a Row item gets its width (spec L-06).</summary>
internal enum RowItemKind
{
    /// <summary>A width in points.</summary>
    Fixed,

    /// <summary>A share of the width left after the fixed and auto items, by weight.</summary>
    Flex,

    /// <summary>The item's own width, measured at an infinite width.</summary>
    Auto,
}
