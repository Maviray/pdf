namespace Maviray.Pdf.Layout;

/// <summary>
/// How much a length may exceed its space and still fit: a millionth of a point, far below what a page shows. Sums of
/// lengths round differently when a container adds them up to measure and when it subtracts them to draw, so every
/// comparison of a length with its space goes through <see cref="Fits"/> (P1.10 review).
/// </summary>
internal static class LayoutTolerance
{
    public const double Points = 1e-6;

    /// <summary>Whether <paramref name="length"/> fits in <paramref name="space"/>, within the tolerance.</summary>
    public static bool Fits(double length, double space) => length <= space + Points;
}
