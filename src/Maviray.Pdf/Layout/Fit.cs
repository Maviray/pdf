namespace Maviray.Pdf.Layout;

/// <summary>How much of an element fits in the space it is measured in (spec L-03).</summary>
internal enum Fit
{
    /// <summary>Everything left of the element fits.</summary>
    Full,

    /// <summary>Part of it fits, and the rest continues on the next page.</summary>
    Partial,

    /// <summary>Nothing of it fits here.</summary>
    None,
}
