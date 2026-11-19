namespace Maviray.Pdf.Writer;

/// <summary>A reference to an indirect object, written as <c>n 0 R</c>. New files always use generation 0.</summary>
internal sealed class PdfReference : PdfObject
{
    public PdfReference(int objectNumber)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(objectNumber, 1);
        ObjectNumber = objectNumber;
    }

    public int ObjectNumber { get; }

    public override void WriteTo(PdfOutput output)
    {
        output.WriteInteger(ObjectNumber);
        output.Write(" 0 R"u8);
    }
}
