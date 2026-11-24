using System.Globalization;

namespace Maviray.Pdf.Tests.Invoice;

/// <summary>
/// The driving invoice (L-17), built only through the public API: a header with the seller, the number, the date and
/// the column headings on every page; the two addresses side by side; the items as a Column of Rows; the totals; and a
/// footer with the bank details, the registration and "Seite X von Y".
/// </summary>
internal static class InvoiceDocument
{
    /// <summary>The page margin on every side, in centimetres.</summary>
    public const double MarginCentimetres = 2;

    public static IReadOnlyList<string> Headings { get; } = ["Pos.", "Beschreibung", "Menge", "Einzelpreis", "USt.", "Betrag"];

    private static readonly Color Grey = Color.FromHex("#666666");

    public static Document Create(InvoiceData invoice, FontRegistry fonts) => Document.Create(document =>
    {
        document.Settings(settings => settings.Fonts(fonts).Deterministic());
        document.Metadata(metadata => metadata
            .Title($"Rechnung {invoice.Number}")
            .Author(invoice.Seller.Name)
            .Language("de-DE")
            .CreationDate(new DateTimeOffset(2026, 11, 24, 9, 0, 0, TimeSpan.FromHours(1))));
        document.Page(page =>
        {
            page.Size(PageSize.A4);
            page.Margin(MarginCentimetres, Unit.Centimetre);
            page.DefaultTextStyle(style => style.FontSize(9));
            page.Header().Column(header => Header(header, invoice));
            page.Content().Column(content => Content(content, invoice));
            page.Footer().Column(footer => Footer(footer, invoice));
        });
    });

    private static void Header(ColumnBuilder header, InvoiceData invoice)
    {
        header.Item().Row(row =>
        {
            row.Flex().Text(invoice.Seller.Name).Bold().FontSize(16);
            row.Auto().AlignRight().Text($"Rechnung {invoice.Number}").Bold().FontSize(12);
        });
        header.Item().AlignRight().Text($"Rechnungsdatum: {invoice.Date}");
        header.Item().PaddingTop(10).PaddingBottom(6).BorderBottom(0.75, Color.Black).PaddingBottom(3)
            .Row(row => Cells(row, Headings, bold: true));
    }

    private static void Content(ColumnBuilder content, InvoiceData invoice)
    {
        content.Spacing(3);
        content.Item().PaddingBottom(10).Row(row =>
        {
            row.Spacing(20);
            row.Flex().Column(column => Address(column, "Absender", invoice.Seller));
            row.Flex().Column(column => Address(column, "Rechnungsempfänger", invoice.Buyer));
        });

        foreach (var item in invoice.Items)
        {
            string[] cells =
            [
                item.Position.ToString(CultureInfo.InvariantCulture),
                item.Description,
                $"{InvoiceAmounts.Quantity(item.Quantity)} {item.Unit}",
                InvoiceAmounts.Format(item.UnitPrice),
                $"{item.VatRate} %",
                InvoiceAmounts.Format(item.Net),
            ];
            content.Item().Row(row => Cells(row, cells, bold: false));
        }

        content.Item().PaddingTop(6).BorderTop(0.75, Color.Black).PaddingTop(4).Column(totals =>
        {
            totals.Spacing(2);
            Total(totals, "Nettobetrag", invoice.Net, bold: false);
            foreach (var rate in invoice.VatRates)
            {
                Total(totals, $"Umsatzsteuer {rate} %", invoice.Vat(rate), bold: false);
            }

            Total(totals, "Gesamtbetrag", invoice.Gross, bold: true);
        });
    }

    private static void Footer(ColumnBuilder footer, InvoiceData invoice)
    {
        footer.Item().PaddingTop(6).BorderTop(0.5, Grey).PaddingTop(4).Text(invoice.Bank).FontSize(7.5).FontColor(Grey);
        footer.Item().Text(invoice.Registration).FontSize(7.5).FontColor(Grey);
        footer.Item().PaddingTop(4).AlignCenter().Text(text =>
        {
            text.Span("Seite ");
            text.PageNumber();
            text.Span(" von ");
            text.PageCount();
        });
    }

    // The six columns: position, description, quantity, unit price, VAT rate and amount. Only the description grows.
    private static void Cells(RowBuilder row, IReadOnlyList<string> cells, bool bold)
    {
        row.Spacing(4);
        Cell(row.Fixed(24), cells[0], bold);
        Cell(row.Flex(), cells[1], bold);
        Cell(row.Fixed(50).AlignRight(), cells[2], bold);
        Cell(row.Fixed(62).AlignRight(), cells[3], bold);
        Cell(row.Fixed(34).AlignRight(), cells[4], bold);
        Cell(row.Fixed(66).AlignRight(), cells[5], bold);
    }

    private static void Cell(IContainer container, string text, bool bold)
    {
        var span = container.Text(text);
        if (bold)
        {
            span.Bold();
        }
    }

    private static void Address(ColumnBuilder column, string label, InvoiceParty party)
    {
        column.Item().Text(label).FontSize(7.5).FontColor(Grey);
        column.Item().Text(party.Name).Bold();
        column.Item().Text(party.Street);
        column.Item().Text(party.City);
        column.Item().Text(party.Contact);
    }

    private static void Total(ColumnBuilder totals, string label, decimal amount, bool bold)
    {
        totals.Item().Row(row =>
        {
            Cell(row.Flex().AlignRight(), label, bold);
            Cell(row.Fixed(90).AlignRight(), InvoiceAmounts.Format(amount), bold);
        });
    }
}
