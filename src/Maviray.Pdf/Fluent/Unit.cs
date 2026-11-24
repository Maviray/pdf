namespace Maviray.Pdf;

/// <summary>A unit of length. Every length in the API is in points unless an overload takes one of these.</summary>
public enum Unit
{
    /// <summary>A point, 1/72 inch, PDF's own unit.</summary>
    Point,

    /// <summary>A millimetre, 72/25.4 points.</summary>
    Millimetre,

    /// <summary>A centimetre, 72/2.54 points.</summary>
    Centimetre,

    /// <summary>An inch, 72 points.</summary>
    Inch,
}
