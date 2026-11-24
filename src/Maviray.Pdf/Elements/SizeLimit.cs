using Maviray.Pdf.Drawing;
using Maviray.Pdf.Layout;

namespace Maviray.Pdf.Elements;

/// <summary>
/// Limits on a child's size (spec L-06), named in paths by the call that made it, such as <c>Width</c> or
/// <c>MaxHeight</c> (P1.10 plan, ruling 4).
/// <list type="bullet">
/// <item>The child gets the available width and height, each capped at its maximum.</item>
/// <item>It is <see cref="Fit.None"/> when the available width or height is below its minimum, so a
/// <c>MinHeight(h)</c> needs h left on the page.</item>
/// <item>Its size is the child's, raised to the minimums. A maximum height gives the child at most that on each
/// page.</item>
/// </list>
/// </summary>
internal sealed class SizeLimit : Element
{
    private const double Tolerance = 1e-9;

    private readonly double _minWidth;
    private readonly double _maxWidth;
    private readonly double _minHeight;
    private readonly double _maxHeight;
    private readonly Element _child;

    /// <param name="name">The element's name in paths.</param>
    /// <param name="minWidth">The least width in points, zero or more and finite.</param>
    /// <param name="maxWidth">The most width the child gets, at least <paramref name="minWidth"/>; infinity for no limit.</param>
    /// <param name="minHeight">The least height in points, zero or more and finite.</param>
    /// <param name="maxHeight">The most height the child gets, at least <paramref name="minHeight"/>; infinity for no limit.</param>
    /// <param name="child">The limited element.</param>
    public SizeLimit(string name, double minWidth, double maxWidth, double minHeight, double maxHeight, Element child)
        : base(name)
    {
        _minWidth = Minimum(minWidth, nameof(minWidth));
        _maxWidth = Maximum(maxWidth, minWidth, nameof(maxWidth));
        _minHeight = Minimum(minHeight, nameof(minHeight));
        _maxHeight = Maximum(maxHeight, minHeight, nameof(maxHeight));
        _child = Adopt(child);
    }

    public override bool ShowsPageCount => _child.ShowsPageCount;

    public static SizeLimit Width(double width, Element child) => new(nameof(Width), Finite(width, nameof(width)), width, 0, double.PositiveInfinity, child);

    public static SizeLimit MinWidth(double width, Element child) => new(nameof(MinWidth), width, double.PositiveInfinity, 0, double.PositiveInfinity, child);

    public static SizeLimit MaxWidth(double width, Element child) => new(nameof(MaxWidth), 0, Finite(width, nameof(width)), 0, double.PositiveInfinity, child);

    public static SizeLimit Height(double height, Element child) => new(nameof(Height), 0, double.PositiveInfinity, Finite(height, nameof(height)), height, child);

    public static SizeLimit MinHeight(double height, Element child) => new(nameof(MinHeight), 0, double.PositiveInfinity, height, double.PositiveInfinity, child);

    public static SizeLimit MaxHeight(double height, Element child) => new(nameof(MaxHeight), 0, double.PositiveInfinity, 0, Finite(height, nameof(height)), child);

    public override Measurement Measure(Size available, PageContext page)
    {
        if (available.Width < _minWidth - Tolerance || available.Height < _minHeight - Tolerance)
        {
            return Measurement.None(this, available);
        }

        var measurement = _child.Measure(Inner(available), page);
        if (measurement.Fit == Fit.None)
        {
            return measurement;
        }

        var size = new Size(Math.Max(measurement.Size.Width, _minWidth), Math.Max(measurement.Size.Height, _minHeight));
        return measurement.Fit == Fit.Full ? Measurement.Full(size, this) : measurement.WithSize(size);
    }

    public override bool Draw(Size available, ICanvas canvas, PageContext page) => _child.Draw(Inner(available), canvas, page);

    public override void Reset() => _child.Reset();

    private static double Minimum(double value, string paramName) => double.IsFinite(value) && value >= 0
        ? value
        : throw new ArgumentOutOfRangeException(paramName, value, "A minimum must be zero or more points, and finite.");

    private static double Maximum(double value, double minimum, string paramName)
    {
        if (double.IsNaN(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "A maximum must be zero or more points, or infinity for none.");
        }

        return value >= minimum ? value : throw new ArgumentException($"The maximum {value} is below the minimum {minimum}.", paramName);
    }

    private static double Finite(double value, string paramName) => double.IsFinite(value) && value >= 0
        ? value
        : throw new ArgumentOutOfRangeException(paramName, value, "The length must be zero or more points, and finite.");

    private Size Inner(Size available) => new(Math.Min(available.Width, _maxWidth), Math.Min(available.Height, _maxHeight));
}
