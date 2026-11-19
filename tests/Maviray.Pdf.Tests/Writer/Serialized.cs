using System.Text;
using Maviray.Pdf.Writer;

namespace Maviray.Pdf.Tests.Writer;

/// <summary>
/// Serializes a PDF object and returns its bytes as Latin-1 text. Latin-1 maps every byte to exactly one character,
/// so comparing the text compares every byte, and a failure message stays readable.
/// </summary>
internal static class Serialized
{
    public static string Of(PdfObject value)
    {
        var stream = new MemoryStream();
        using (var output = new PdfOutput(stream))
        {
            value.WriteTo(output);
            output.Flush();
        }

        return Encoding.Latin1.GetString(stream.ToArray());
    }
}
