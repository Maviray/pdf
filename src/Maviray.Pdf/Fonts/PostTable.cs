namespace Maviray.Pdf.Fonts;

/// <summary>The values the library reads from the header of <c>post</c>, which is optional (spec F-03).</summary>
internal sealed record PostTable(double ItalicAngle, int UnderlinePosition, int UnderlineThickness, bool IsFixedPitch)
{
    private const int HeaderLength = 32;

    public static PostTable Parse(ReadOnlySpan<byte> table)
    {
        var reader = new FontTableReader(table, "post");
        if (reader.Length < HeaderLength)
        {
            throw reader.Error($"is too short: its header needs {HeaderLength} bytes, and it has {reader.Length}");
        }

        // italicAngle is a 16.16 fixed-point number.
        return new PostTable(reader.Int32(4) / 65536.0, reader.Int16(8), reader.Int16(10), reader.UInt32(12) != 0);
    }
}
