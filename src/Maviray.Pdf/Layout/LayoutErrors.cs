using System.Globalization;

namespace Maviray.Pdf.Layout;

/// <summary>Writes lengths and sizes for <see cref="LayoutException"/> messages, the same under every culture.</summary>
internal static class LayoutErrors
{
    /// <summary>A length in points with at most two decimals, such as <c>31.2 pt</c>.</summary>
    public static string Length(double points) => Number(points) + " pt";

    /// <summary>A size in points, such as <c>451.28 × 722 pt</c>. An infinite width, which a Row gives an Auto item
    /// (L-06), is written as ∞.</summary>
    public static string Size(Size size) => Number(size.Width) + " × " + Number(size.Height) + " pt";

    private static string Number(double points) =>
        double.IsPositiveInfinity(points) ? "∞" : points.ToString("0.##", CultureInfo.InvariantCulture);
}
