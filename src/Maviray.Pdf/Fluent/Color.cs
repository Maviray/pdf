using Maviray.Pdf.Writer;

namespace Maviray.Pdf;

/// <summary>A colour in RGB, each component from 0 to 255.</summary>
public readonly struct Color : IEquatable<Color>
{
    private Color(byte r, byte g, byte b)
    {
        R = r;
        G = g;
        B = b;
    }

    /// <summary>Black, <c>#000000</c>.</summary>
    public static Color Black { get; } = new(0, 0, 0);

    /// <summary>White, <c>#FFFFFF</c>.</summary>
    public static Color White { get; } = new(255, 255, 255);

    /// <summary>The red component, from 0 to 255.</summary>
    public byte R { get; }

    /// <summary>The green component, from 0 to 255.</summary>
    public byte G { get; }

    /// <summary>The blue component, from 0 to 255.</summary>
    public byte B { get; }

    /// <summary>The two colours are equal when all three components are.</summary>
    public static bool operator ==(Color left, Color right) => left.Equals(right);

    /// <summary>The two colours differ in at least one component.</summary>
    public static bool operator !=(Color left, Color right) => !left.Equals(right);

    /// <summary>A colour from its red, green and blue components.</summary>
    /// <param name="r">The red component, from 0 to 255.</param>
    /// <param name="g">The green component, from 0 to 255.</param>
    /// <param name="b">The blue component, from 0 to 255.</param>
    public static Color FromRgb(byte r, byte g, byte b) => new(r, g, b);

    /// <summary>A colour from hexadecimal text: <c>#RRGGBB</c>, or <c>#RGB</c> with each digit doubled, in either case.</summary>
    /// <param name="hex">The text, starting with <c>#</c>, such as <c>#1F4E79</c> or <c>#ABC</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="hex"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="hex"/> isn't <c>#</c> followed by 3 or 6 hexadecimal digits.</exception>
    public static Color FromHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        var digits = hex.Length > 0 && hex[0] == '#' ? hex.AsSpan(1) : [];
        if (hex.Length == 0 || hex[0] != '#' || (digits.Length != 3 && digits.Length != 6) || !IsHex(digits))
        {
            throw new ArgumentException($"'{hex}' isn't a colour: write # and 6 hexadecimal digits, such as #1F4E79, or 3, such as #ABC.", nameof(hex));
        }

        return digits.Length == 3
            ? new((byte)(Digit(digits[0]) * 17), (byte)(Digit(digits[1]) * 17), (byte)(Digit(digits[2]) * 17))
            : new((byte)((Digit(digits[0]) * 16) + Digit(digits[1])), (byte)((Digit(digits[2]) * 16) + Digit(digits[3])), (byte)((Digit(digits[4]) * 16) + Digit(digits[5])));
    }

    /// <summary>Whether <paramref name="other"/> has the same three components.</summary>
    /// <param name="other">The colour to compare with.</param>
    public bool Equals(Color other) => R == other.R && G == other.G && B == other.B;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Color other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => (R << 16) | (G << 8) | B;

    /// <summary>The colour as <c>#RRGGBB</c>.</summary>
    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";

    /// <summary>The colour in the writer's DeviceRGB components, each from 0 to 1.</summary>
    internal RgbColor ToRgb() => new(R / 255.0, G / 255.0, B / 255.0);

    private static bool IsHex(ReadOnlySpan<char> digits)
    {
        foreach (var c in digits)
        {
            if (!char.IsAsciiHexDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    private static int Digit(char c) => c <= '9' ? c - '0' : (c | 0x20) - 'a' + 10;
}
