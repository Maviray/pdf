using System.Globalization;
using System.Text;

namespace Maviray.Pdf.Tests.Validation;

/// <summary>
/// A binary PPM (P6) image with 8 bits per channel: pdftoppm's default output, so the tests need no image library.
/// </summary>
internal sealed class PpmImage
{
    private readonly byte[] _pixels;

    private PpmImage(int width, int height, byte[] pixels)
    {
        Width = width;
        Height = height;
        _pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public (byte R, byte G, byte B) GetPixel(int x, int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, Width);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Height);

        var index = ((y * Width) + x) * 3;
        return (_pixels[index], _pixels[index + 1], _pixels[index + 2]);
    }

    /// <exception cref="FormatException">The bytes aren't a binary PPM with a maximum value of 255.</exception>
    public static PpmImage Parse(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        var position = 0;
        var magic = NextToken(bytes, ref position);
        if (magic != "P6")
        {
            throw new FormatException($"Expected a binary PPM (P6), found '{magic}'.");
        }

        var width = int.Parse(NextToken(bytes, ref position), CultureInfo.InvariantCulture);
        var height = int.Parse(NextToken(bytes, ref position), CultureInfo.InvariantCulture);
        var maxValue = NextToken(bytes, ref position);
        if (maxValue != "255")
        {
            throw new FormatException($"Expected a maximum value of 255, found '{maxValue}'.");
        }

        position++; // Exactly one whitespace byte separates the header from the pixels.
        var length = width * height * 3;
        if (bytes.Length - position < length)
        {
            throw new FormatException($"The image needs {length} bytes of pixels but has {bytes.Length - position}.");
        }

        return new PpmImage(width, height, bytes[position..(position + length)]);
    }

    private static string NextToken(byte[] bytes, ref int position)
    {
        while (position < bytes.Length && (IsWhitespace(bytes[position]) || bytes[position] == '#'))
        {
            if (bytes[position] == '#')
            {
                while (position < bytes.Length && bytes[position] != '\n')
                {
                    position++;
                }
            }
            else
            {
                position++;
            }
        }

        var start = position;
        while (position < bytes.Length && !IsWhitespace(bytes[position]))
        {
            position++;
        }

        return Encoding.ASCII.GetString(bytes, start, position - start);
    }

    private static bool IsWhitespace(byte value) => value is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r';
}
