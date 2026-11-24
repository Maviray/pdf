namespace Maviray.Pdf.Fluent.Nodes;

/// <summary>Lengths in the API's units, turned into points and checked at the call (L-02).</summary>
internal static class Units
{
    /// <summary><paramref name="value"/> in points: zero or more, and finite.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The unit isn't defined, or the length is negative or not finite.</exception>
    public static double ToPoints(double value, Unit unit, string paramName)
    {
        if (!Enum.IsDefined(unit))
        {
            throw new ArgumentOutOfRangeException(nameof(unit), unit, "The unit isn't a defined value.");
        }

        if (!(double.IsFinite(value) && value >= 0))
        {
            throw new ArgumentOutOfRangeException(paramName, value, "A length must be zero or more, and finite.");
        }

        return unit switch
        {
            Unit.Millimetre => value * 72 / 25.4,
            Unit.Centimetre => value * 72 / 2.54,
            Unit.Inch => value * 72,
            _ => value,
        };
    }
}
