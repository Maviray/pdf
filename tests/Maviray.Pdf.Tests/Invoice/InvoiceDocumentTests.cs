using System.Globalization;

namespace Maviray.Pdf.Tests.Invoice;

// The driving invoice's data and amounts (L-17), before the gate checks the document they make.
public sealed class InvoiceDocumentTests
{
    [Theory]
    [InlineData("1234.5", "1.234,50 €")]
    [InlineData("0", "0,00 €")]
    [InlineData("1234567.891", "1.234.567,89 €")]
    [InlineData("0.005", "0,01 €")]
    public void AmountsAreGerman(string value, string expected) =>
        Assert.Equal(expected, InvoiceAmounts.Format(decimal.Parse(value, CultureInfo.InvariantCulture)));

    [Fact]
    public void TheTotalsAddUp()
    {
        var invoice = InvoiceSample.Create() with
        {
            Items = [new InvoiceItem(1, "A", 2, "Stk.", 100m, 19), new InvoiceItem(2, "B", 3, "Std.", 10.50m, 7)],
        };

        Assert.Equal((231.50m, 38.00m, 2.21m, 271.71m), (invoice.Net, invoice.Vat(19), invoice.Vat(7), invoice.Gross));
    }

    [Fact]
    public void TheDescriptionsAreUniqueAndNoneHoldsAnother()
    {
        var descriptions = InvoiceSample.Create().Items.Select(item => item.Description).ToList();

        Assert.All(descriptions, description => Assert.Single(descriptions, other => other.Contains(description, StringComparison.Ordinal)));
    }

    [Fact]
    public void TheTurkishAndCyrillicStringsAreInTheData()
    {
        var invoice = InvoiceSample.Create();
        var text = string.Join("\n", [invoice.Buyer.Name, invoice.Buyer.Street, invoice.Buyer.City, invoice.Buyer.Contact, .. invoice.Items.Select(item => item.Description)]);

        Assert.All(InvoiceSample.TurkishAndCyrillic, value => Assert.Contains(value, text, StringComparison.Ordinal));
        Assert.Contains(InvoiceSample.TurkishAndCyrillic, value => value.Any(c => c is >= 'Ѐ' and <= 'ӿ'));
        Assert.Contains(InvoiceSample.TurkishAndCyrillic, value => value.Any(c => c is 'İ' or 'ı' or 'ş' or 'ğ'));
    }
}
