using System.Globalization;

namespace Maviray.Pdf.Layout;

/// <summary>Builds <see cref="LayoutException"/>s, and writes their lengths and sizes the same under every culture.</summary>
internal static class LayoutErrors
{
    /// <summary>
    /// The error for <paramref name="source"/> on <paramref name="page"/>: its path with <c>Page n</c> first, the
    /// reason, and the space it had (L-05). An element that can never fit throws it from <c>Measure</c>.
    /// </summary>
    public static LayoutException Error(PageContext page, Element source, string reason, Size space)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(source);
        return new($"Page {page.PageNumber} > {source.Path}", reason, space.Width, space.Height);
    }

    /// <summary>A length in points with at most two decimals, such as <c>31.2 pt</c>.</summary>
    public static string Length(double points) => Number(points) + " pt";

    /// <summary>A size in points, such as <c>451.28 × 722 pt</c>. An infinite width, which a Row gives an Auto item
    /// (L-06), is written as ∞.</summary>
    public static string Size(Size size) => Number(size.Width) + " × " + Number(size.Height) + " pt";

    private static string Number(double points) =>
        double.IsPositiveInfinity(points) ? "∞" : points.ToString("0.##", CultureInfo.InvariantCulture);
}
