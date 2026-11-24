namespace Maviray.Pdf.Text;

/// <summary>What UAX #14 allows at a boundary between two code points (spec T-06).</summary>
internal enum LineBreakKind : byte
{
    /// <summary>The line may not break here.</summary>
    None = 0,

    /// <summary>The line may break here.</summary>
    Allowed = 1,

    /// <summary>The line must break here: after a break character, or at the end of the text.</summary>
    Required = 2,
}
