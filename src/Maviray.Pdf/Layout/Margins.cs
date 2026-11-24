namespace Maviray.Pdf.Layout;

/// <summary>A page's margins in points, each zero or more (spec L-04).</summary>
internal readonly record struct Margins(double Left, double Top, double Right, double Bottom);
