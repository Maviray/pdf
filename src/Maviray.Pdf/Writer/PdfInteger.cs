namespace Maviray.Pdf.Writer;

/// <summary>A PDF integer, written as invariant decimal digits.</summary>
internal sealed class PdfInteger : PdfObject
{
    public PdfInteger(long value)
    {
        Value = value;
    }

    public long Value { get; }

    public override void WriteTo(PdfOutput output) => output.WriteInteger(Value);
}
