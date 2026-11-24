namespace Maviray.Pdf.Tests.Invoice;

/// <summary>A seller or buyer of the driving invoice: synthetic, like all its data.</summary>
internal sealed record InvoiceParty(string Name, string Street, string City, string Contact);
