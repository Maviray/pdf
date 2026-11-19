using System.Globalization;

namespace Maviray.Pdf.Writer;

/// <summary>
/// Formats real numbers the one way the writer emits them (spec D-03): invariant culture, at most four decimals with
/// decimal midpoints rounded away from zero, no exponent, no trailing zeros, and never <c>-0</c>.
/// </summary>
/// <remarks>
/// The custom format string does the rounding: .NET takes the value's 15-significant-digit decimal form and rounds it
/// away from zero, so <c>0.00015</c> gives <c>0.0002</c> even though the nearest double lies just below the midpoint.
/// <see cref="Math.Round(double, int, MidpointRounding)"/> isn't used, because it rounds the binary value, which gets
/// such cases wrong and gives different results on different .NET versions. Because of the 15 digits, values of 1e11
/// and above get fewer than four decimals. PDF coordinates stay far below that.
/// </remarks>
internal static class PdfNumberFormat
{
    /// <summary>Enough for the largest allowed value: a sign, 39 digits, a point and four decimals.</summary>
    public const int MaxLength = 48;

    /// <summary>Writes <paramref name="value"/> into <paramref name="destination"/> and returns the bytes written.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is NaN, infinite, or larger in magnitude than
    /// <see cref="float.MaxValue"/>, the largest real a PDF 1.7 reader must accept.</exception>
    public static int Format(double value, Span<byte> destination)
    {
        ThrowIfOutOfRange(value, nameof(value));

        if (!value.TryFormat(destination, out var written, "0.####", CultureInfo.InvariantCulture))
        {
            throw new ArgumentException($"The destination needs room for {MaxLength} bytes.", nameof(destination));
        }

        // A negative value that rounds to zero, and -0 itself, come out as "-0". PDF gets a plain 0.
        if (written == 2 && destination[0] == (byte)'-' && destination[1] == (byte)'0')
        {
            destination[0] = (byte)'0';
            written = 1;
        }

        return written;
    }

    /// <summary>Throws unless <paramref name="value"/> is a number a PDF can hold.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is NaN, infinite, or larger in magnitude than
    /// <see cref="float.MaxValue"/>.</exception>
    public static void ThrowIfOutOfRange(double value, string paramName)
    {
        if (!double.IsFinite(value) || Math.Abs(value) > float.MaxValue)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "A PDF number must be finite and no larger in magnitude than 3.4028235E+38.");
        }
    }
}
