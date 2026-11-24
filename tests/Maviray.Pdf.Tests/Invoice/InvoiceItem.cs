namespace Maviray.Pdf.Tests.Invoice;

/// <summary>A line item: its position, description, quantity and unit, unit price and VAT rate in percent.</summary>
internal sealed record InvoiceItem(int Position, string Description, decimal Quantity, string Unit, decimal UnitPrice, int VatRate)
{
    /// <summary>The net amount, rounded to the cent.</summary>
    public decimal Net => Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
}
