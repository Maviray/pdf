using System.Globalization;

namespace Maviray.Pdf.Writer;

/// <summary>The PDF date format (spec L-12), such as <c>D:20261123140500+01'00'</c>, or with <c>Z</c> for UTC.</summary>
internal static class PdfDate
{
    public static string Format(DateTimeOffset value)
    {
        var date = value.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var offset = value.Offset;
        if (offset == TimeSpan.Zero)
        {
            return $"D:{date}Z";
        }

        var sign = offset < TimeSpan.Zero ? '-' : '+';
        var magnitude = offset.Duration();
        return string.Create(CultureInfo.InvariantCulture, $"D:{date}{sign}{magnitude.Hours:D2}'{magnitude.Minutes:D2}'");
    }
}
