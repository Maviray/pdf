namespace Maviray.Pdf.Writer;

/// <summary>
/// A PDF stream: a dictionary and its data. The writer owns <c>/Length</c> and <c>/Filter</c> (spec D-08), so the
/// dictionary must not contain them; <c>/Length</c> is added when the stream is written.
/// </summary>
internal sealed class PdfStream : PdfObject
{
    private const string LengthAndFilterMessage = "The writer sets /Length and /Filter; the stream's dictionary must not contain them.";

    /// <exception cref="ArgumentException"><paramref name="dictionary"/> contains <c>/Length</c> or <c>/Filter</c>.</exception>
    public PdfStream(PdfDictionary dictionary, ReadOnlyMemory<byte> data)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        if (SetsLengthOrFilter(dictionary))
        {
            throw new ArgumentException(LengthAndFilterMessage, nameof(dictionary));
        }

        Dictionary = dictionary;
        Data = data;
    }

    public PdfDictionary Dictionary { get; }

    public ReadOnlyMemory<byte> Data { get; }

    /// <exception cref="InvalidOperationException">The dictionary gained <c>/Length</c> or <c>/Filter</c> after the stream
    /// was created; writing it would duplicate the key.</exception>
    public override void WriteTo(PdfOutput output)
    {
        if (SetsLengthOrFilter(Dictionary))
        {
            throw new InvalidOperationException(LengthAndFilterMessage);
        }

        output.Write("<<"u8);
        Dictionary.WriteEntries(output);
        if (Dictionary.Count > 0)
        {
            output.WriteByte((byte)' ');
        }

        PdfName.Length.WriteTo(output);
        output.WriteByte((byte)' ');
        output.WriteInteger(Data.Length);
        output.Write(">>\nstream\n"u8);
        output.Write(Data.Span);
        output.Write("\nendstream"u8);
    }

    private static bool SetsLengthOrFilter(PdfDictionary dictionary) =>
        dictionary.ContainsKey(PdfName.Length) || dictionary.ContainsKey(PdfName.Filter);
}
