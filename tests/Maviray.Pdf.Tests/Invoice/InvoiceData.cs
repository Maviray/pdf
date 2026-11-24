namespace Maviray.Pdf.Tests.Invoice;

/// <summary>The driving invoice's data (L-17): its number and date, the parties, the line items and the footer's lines.</summary>
internal sealed record InvoiceData(
    string Number,
    string Date,
    InvoiceParty Seller,
    InvoiceParty Buyer,
    IReadOnlyList<InvoiceItem> Items,
    string Bank,
    string Registration)
{
    /// <summary>The sum of the items' net amounts.</summary>
    public decimal Net => Items.Sum(item => item.Net);

    /// <summary>The VAT rates the items use, highest first.</summary>
    public IReadOnlyList<int> VatRates => [.. Items.Select(item => item.VatRate).Distinct().OrderDescending()];

    /// <summary>The gross total: the net sum and the VAT of every rate.</summary>
    public decimal Gross => Net + VatRates.Sum(Vat);

    /// <summary>The VAT at <paramref name="rate"/> percent, on the net sum of the items at that rate, rounded to the cent.</summary>
    public decimal Vat(int rate) =>
        Math.Round(Items.Where(item => item.VatRate == rate).Sum(item => item.Net) * rate / 100, 2, MidpointRounding.AwayFromZero);
}
