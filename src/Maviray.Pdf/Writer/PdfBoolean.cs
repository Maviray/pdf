namespace Maviray.Pdf.Writer;

/// <summary>A PDF boolean, written as <c>true</c> or <c>false</c>.</summary>
internal sealed class PdfBoolean : PdfObject
{
    private PdfBoolean(bool value)
    {
        Value = value;
    }

    public static PdfBoolean True { get; } = new(true);

    public static PdfBoolean False { get; } = new(false);

    public bool Value { get; }

    public override void WriteTo(PdfOutput output) => output.Write(Value ? "true"u8 : "false"u8);
}
