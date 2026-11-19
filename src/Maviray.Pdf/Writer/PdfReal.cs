namespace Maviray.Pdf.Writer;

/// <summary>A PDF real number, written by <see cref="PdfNumberFormat"/>.</summary>
internal sealed class PdfReal : PdfObject
{
    /// <exception cref="ArgumentOutOfRangeException">The value is NaN, infinite, or larger in magnitude than
    /// <see cref="float.MaxValue"/>.</exception>
    public PdfReal(double value)
    {
        PdfNumberFormat.ThrowIfOutOfRange(value, nameof(value));
        Value = value;
    }

    public double Value { get; }

    public override void WriteTo(PdfOutput output) => output.WriteNumber(Value);
}
