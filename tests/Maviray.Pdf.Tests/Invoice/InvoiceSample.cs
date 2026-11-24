namespace Maviray.Pdf.Tests.Invoice;

/// <summary>
/// The driving invoice's synthetic data (L-17): German, with Turkish and Cyrillic names in the buyer and the item
/// descriptions. Each description ends with a three-digit lot number, so none holds another, and fits on one line.
/// </summary>
internal static class InvoiceSample
{
    /// <summary>The item count that makes the document run to exactly 10 pages, with the totals on the last.</summary>
    public const int ItemCount = 420;

    private static readonly (string Text, string Unit, decimal UnitPrice, int VatRate)[] Templates =
    [
        ("Wartung Serverraum Köln", "Std.", 95.00m, 19),
        ("Fachbuch Netzwerktechnik", "Stk.", 49.90m, 7),
        ("Schulung Ayşe Kılıç", "Tag", 680.00m, 19),
        ("Übersetzung Наталья Ковалёва", "Seite", 32.50m, 19),
        ("Lizenz Backup-Software", "Stk.", 12.00m, 19),
        ("Beratung Projekt Şişli", "Std.", 120.00m, 19),
        ("Zeitschrift «Сети и связь»", "Heft", 8.90m, 7),
        ("Fahrtkosten nach Iğdır", "km", 0.30m, 19),
        ("Datensicherung Юрий Соловьёв", "Std.", 95.00m, 19),
        ("Ersatzteil Lüfter 120 mm", "Stk.", 18.40m, 19),
    ];

    /// <summary>The strings beyond German that must copy out of the document exactly.</summary>
    public static IReadOnlyList<string> TurkishAndCyrillic { get; } =
    [
        "Yıldız Lojistik GmbH",
        "İnönü-Straße 14",
        "z. Hd. Ayşe Kılıç",
        "Schulung Ayşe Kılıç",
        "Übersetzung Наталья Ковалёва",
        "Beratung Projekt Şişli",
        "Zeitschrift «Сети и связь»",
        "Fahrtkosten nach Iğdır",
        "Datensicherung Юрий Соловьёв",
    ];

    public static InvoiceData Create(int itemCount = ItemCount) => new(
        "RE-2026-0042",
        "24.11.2026",
        new InvoiceParty("Maviray GmbH", "Hohenzollernring 72", "50672 Köln", "Tel. 0221 4711-0"),
        new InvoiceParty("Yıldız Lojistik GmbH", "İnönü-Straße 14", "40210 Düsseldorf", "z. Hd. Ayşe Kılıç"),
        [.. Enumerable.Range(1, itemCount).Select(Item)],
        "Sparkasse KölnBonn · IBAN DE89 3704 0044 0532 0130 00 · BIC COLSDE33",
        "Amtsgericht Köln HRB 99999 · USt-IdNr. DE999999999 · Geschäftsführung: Erika Mustermann");

    private static InvoiceItem Item(int position)
    {
        var (text, unit, unitPrice, vatRate) = Templates[(position - 1) % Templates.Length];
        var quantity = unit == "km" ? 40 + (position % 60) : 1 + (position % 4);
        return new InvoiceItem(position, $"{text}, Los {position:000}", quantity, unit, unitPrice, vatRate);
    }
}
