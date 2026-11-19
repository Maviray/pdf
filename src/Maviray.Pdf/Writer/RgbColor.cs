namespace Maviray.Pdf.Writer;

/// <summary>
/// A colour in the DeviceRGB space, each component from 0 to 1 (spec D-09). The public <c>Color</c> type of P1 maps
/// onto it.
/// </summary>
internal readonly record struct RgbColor
{
    /// <exception cref="ArgumentOutOfRangeException">A component is below 0, above 1, or NaN.</exception>
    public RgbColor(double red, double green, double blue)
    {
        Red = Component(red, nameof(red));
        Green = Component(green, nameof(green));
        Blue = Component(blue, nameof(blue));
    }

    public double Red { get; }

    public double Green { get; }

    public double Blue { get; }

    private static double Component(double value, string paramName)
    {
        // Written so that NaN fails too: every comparison with NaN is false.
        if (!(value >= 0 && value <= 1))
        {
            throw new ArgumentOutOfRangeException(paramName, value, "A colour component must be from 0 to 1.");
        }

        return value;
    }
}
