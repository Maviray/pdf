using System.Text;

namespace Maviray.Pdf.Writer;

/// <summary>A PDF string: a sequence of bytes, written as a literal <c>(…)</c> or as hex <c>&lt;…&gt;</c> (spec D-02).</summary>
internal sealed class PdfString : PdfObject
{
    public PdfString(ReadOnlyMemory<byte> bytes, PdfStringForm form = PdfStringForm.Literal)
    {
        Bytes = bytes;
        Form = form;
    }

    public ReadOnlyMemory<byte> Bytes { get; }

    public PdfStringForm Form { get; }

    /// <summary>
    /// A PDF text string. Printable ASCII becomes a literal string of those bytes; anything else becomes UTF-16BE
    /// with the <c>FE FF</c> byte-order mark, written as hex.
    /// </summary>
    public static PdfString FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!text.AsSpan().ContainsAnyExceptInRange((char)0x20, (char)0x7E))
        {
            return new PdfString(Encoding.ASCII.GetBytes(text));
        }

        var bytes = new byte[2 + Encoding.BigEndianUnicode.GetByteCount(text)];
        bytes[0] = 0xFE;
        bytes[1] = 0xFF;
        Encoding.BigEndianUnicode.GetBytes(text, bytes.AsSpan(2));
        return new PdfString(bytes, PdfStringForm.Hex);
    }

    /// <summary>
    /// Writes <paramref name="bytes"/> as a literal string. Backslash and both parentheses are always escaped, so the
    /// string never depends on balanced parentheses; control bytes are escaped too, so no line ending can be altered.
    /// </summary>
    public static void WriteLiteral(PdfOutput output, ReadOnlySpan<byte> bytes)
    {
        output.WriteByte((byte)'(');
        foreach (var value in bytes)
        {
            switch (value)
            {
                case (byte)'\\':
                    output.Write("\\\\"u8);
                    break;
                case (byte)'(':
                    output.Write("\\("u8);
                    break;
                case (byte)')':
                    output.Write("\\)"u8);
                    break;
                case (byte)'\r':
                    output.Write("\\r"u8);
                    break;
                case (byte)'\n':
                    output.Write("\\n"u8);
                    break;
                case (byte)'\t':
                    output.Write("\\t"u8);
                    break;
                case (byte)'\b':
                    output.Write("\\b"u8);
                    break;
                case (byte)'\f':
                    output.Write("\\f"u8);
                    break;
                case < 0x20 or 0x7F:
                    output.WriteByte((byte)'\\');
                    output.WriteByte((byte)('0' + (value >> 6)));
                    output.WriteByte((byte)('0' + ((value >> 3) & 7)));
                    output.WriteByte((byte)('0' + (value & 7)));
                    break;
                default:
                    output.WriteByte(value);
                    break;
            }
        }

        output.WriteByte((byte)')');
    }

    public override void WriteTo(PdfOutput output)
    {
        if (Form == PdfStringForm.Literal)
        {
            WriteLiteral(output, Bytes.Span);
            return;
        }

        output.WriteByte((byte)'<');
        foreach (var value in Bytes.Span)
        {
            output.WriteHex(value);
        }

        output.WriteByte((byte)'>');
    }
}
