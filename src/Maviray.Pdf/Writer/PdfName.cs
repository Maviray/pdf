using System.Text;

namespace Maviray.Pdf.Writer;

/// <summary>
/// A PDF name, written as <c>/</c> followed by its UTF-8 bytes. Any byte outside <c>0x21</c>–<c>0x7E</c>, <c>#</c>,
/// and the delimiters <c>( ) &lt; &gt; [ ] { } / %</c> are written as <c>#XX</c> in uppercase hex (spec D-02).
/// </summary>
internal sealed class PdfName : PdfObject, IEquatable<PdfName>
{
    private readonly byte[] _bytes;

    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is empty.</exception>
    public PdfName(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        Value = value;
        _bytes = Encoding.UTF8.GetBytes(value);
    }

    public static PdfName Length { get; } = new("Length");

    public static PdfName Filter { get; } = new("Filter");

    public string Value { get; }

    private static ReadOnlySpan<byte> EscapedPrintables => "#()<>[]{}/%"u8;

    public override void WriteTo(PdfOutput output)
    {
        output.WriteByte((byte)'/');
        foreach (var value in _bytes)
        {
            if (value is >= 0x21 and <= 0x7E && !EscapedPrintables.Contains(value))
            {
                output.WriteByte(value);
            }
            else
            {
                output.WriteByte((byte)'#');
                output.WriteHex(value);
            }
        }
    }

    public bool Equals(PdfName? other) => other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as PdfName);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
}
