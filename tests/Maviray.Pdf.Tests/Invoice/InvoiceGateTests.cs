using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Maviray.Pdf.Tests.Validation;

namespace Maviray.Pdf.Tests.Invoice;

/// <summary>
/// The P1 layout gate (L-17): the driving invoice, ten pages of it, with every check. The file is saved as
/// <c>InvoiceGatePassesEveryCheck.pdf</c>, with the strings page 1 must copy out beside it as
/// <c>InvoiceGatePassesEveryCheck.txt</c>. <c>docs/qa/checklists/viewer-check.md</c> describes the file for the check by
/// hand. Keep this test's name, the layout and the checklist in step: the test pins the items on each page and the
/// totals the checklist states.
/// </summary>
public sealed class InvoiceGateTests
{
    private const int Pages = 10;

    // Each page's first and last item, as the checklist states them.
    private static readonly (int First, int Last)[] PageItems =
        [(1, 35), (36, 75), (76, 115), (116, 155), (156, 195), (196, 235), (236, 275), (276, 315), (316, 355), (356, 385)];

    // The totals on page 10, as the checklist states them.
    private static readonly string[] Totals =
        ["Nettobetrag", "122.279,80 €", "Umsatzsteuer 19 %", "22.291,39 €", "Umsatzsteuer 7 %", "346,97 €", "Gesamtbetrag", "144.918,16 €"];

    private static readonly Regex Lot = new(@", Los (\d{3})", RegexOptions.CultureInvariant);

    // The column headings on one line, in order, as pdftotext's physical layout writes them.
    private static readonly Regex HeadingLine = new(
        string.Join(@"\s+", InvoiceDocument.Headings.Select(Regex.Escape)),
        RegexOptions.CultureInvariant);

    [Fact]
    public void InvoiceGatePassesEveryCheck()
    {
        var invoice = InvoiceSample.Create();
        var bytes = InvoiceDocument.Create(invoice, new FontRegistry().RegisterNotoSans()).ToBytes();
        var path = GeneratedFiles.Save(bytes);
        var pageOne = PageOneStrings(invoice);
        GeneratedFiles.SaveAs(Encoding.UTF8.GetBytes(string.Join('\n', pageOne) + "\n"), ".txt");

        // 1. Ten pages.
        Assert.Equal(Pages.ToString(CultureInfo.InvariantCulture), Poppler.Info(path)["Pages"]);

        // 2. Every page has the header, the column headings and its footer.
        var pages = Poppler.ExtractText(path, physicalLayout: true).Split('\f');
        Assert.Equal(Pages, pages.Length);
        for (var n = 1; n <= Pages; n++)
        {
            var page = pages[n - 1];
            Assert.Contains(invoice.Seller.Name, page, StringComparison.Ordinal);
            Assert.Contains($"Rechnung {invoice.Number}", page, StringComparison.Ordinal);
            Assert.Contains($"Rechnungsdatum: {invoice.Date}", page, StringComparison.Ordinal);
            Assert.Matches(HeadingLine, page);
            Assert.Contains($"Seite {n} von {Pages}", page, StringComparison.Ordinal);
            Assert.Contains(invoice.Bank, page, StringComparison.Ordinal);
            Assert.Contains(invoice.Registration, page, StringComparison.Ordinal);
        }

        // 3. Every item's description once, in order, across the page breaks; the totals on the last page only.
        var all = string.Join("\n", pages);
        var from = 0;
        foreach (var item in invoice.Items)
        {
            var at = all.IndexOf(item.Description, from, StringComparison.Ordinal);
            Assert.True(at >= 0, $"Item {item.Position}, '{item.Description}', is missing or out of order.");
            Assert.True(
                at == all.IndexOf(item.Description, StringComparison.Ordinal) && at == all.LastIndexOf(item.Description, StringComparison.Ordinal),
                $"Item {item.Position} appears twice.");
            from = at + item.Description.Length;
        }

        var lots = pages.Select(page => Lot.Matches(page).Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)).ToList());
        Assert.Equal(PageItems, lots.Select(page => (page[0], page[^1])));
        Assert.All(Totals, total => Assert.Contains(total, pages[^1], StringComparison.Ordinal));
        Assert.Contains(InvoiceAmounts.Format(invoice.Gross), pages[^1], StringComparison.Ordinal);
        Assert.All(pages[..^1], page => Assert.DoesNotContain("Nettobetrag", page, StringComparison.Ordinal));
        Assert.All(pageOne, value => Assert.Contains(value, pages[0], StringComparison.Ordinal));

        // 4. Every Turkish and Cyrillic string copies out exactly.
        Assert.All(InvoiceSample.TurkishAndCyrillic, value => Assert.Contains(value, all, StringComparison.Ordinal));

        // 5. qpdf accepts the file, and every font is embedded, subset and mapped to Unicode.
        Qpdf.Check(path);
        Assert.All(Poppler.ListFonts(path), font => Assert.True(font.Embedded && font.Subset && font.Unicode, font.ToString()));

        // 6. No ink outside the margins, at 72 dpi, where a pixel is a point.
        var margin = InvoiceDocument.MarginCentimetres * 72 / 2.54;
        var (left, top) = ((int)Math.Floor(margin), (int)Math.Floor(margin));
        var (right, bottom) = ((int)Math.Ceiling(PageSize.A4.Width - margin), (int)Math.Ceiling(PageSize.A4.Height - margin));
        var renders = Poppler.Render(path);
        Assert.Equal(Pages, renders.Count);
        for (var n = 1; n <= Pages; n++)
        {
            var stray = InkOutside(renders[n - 1], left, top, right, bottom);
            Assert.True(stray is null, $"Page {n} has ink outside its margins, first at {stray}.");
        }

        // 7. Deterministic: fresh data and a fresh registry give the same bytes.
        Assert.Equal(bytes, InvoiceDocument.Create(InvoiceSample.Create(), new FontRegistry().RegisterNotoSans()).ToBytes());
    }

    // What page 1 must copy out in a viewer: the header, both addresses, the first items and the footer's page line.
    private static List<string> PageOneStrings(InvoiceData invoice) =>
    [
        invoice.Seller.Name,
        $"Rechnung {invoice.Number}",
        $"Rechnungsdatum: {invoice.Date}",
        invoice.Buyer.Name,
        invoice.Buyer.Street,
        invoice.Buyer.City,
        invoice.Buyer.Contact,
        .. invoice.Items.Take(10).Select(item => item.Description),
        $"Seite 1 von {Pages}",
    ];

    // The first pixel with a channel under 128 outside the box from (left, top) to (right, bottom), or null.
    private static (int X, int Y)? InkOutside(PpmImage render, int left, int top, int right, int bottom)
    {
        for (var y = 0; y < render.Height; y++)
        {
            for (var x = 0; x < render.Width; x++)
            {
                if (x >= left && x < right && y >= top && y < bottom)
                {
                    continue;
                }

                var (r, g, b) = render.GetPixel(x, y);
                if (r < 128 || g < 128 || b < 128)
                {
                    return (x, y);
                }
            }
        }

        return null;
    }
}
