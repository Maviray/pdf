using Maviray.Pdf.Fonts;
using Maviray.Pdf.Text;

namespace Maviray.Pdf.Tests.Text;

/// <summary>
/// The text gate's strings (spec T-15), as the P1.8 plan fixes them. Every character beyond ASCII is an escape, as in
/// every text test. The plan and the viewer checklist show them readable.
/// </summary>
internal static class TextGateText
{
    /// <summary>
    /// The line height of every gate style. At Noto Sans's normal 1.362, the six paragraphs need more than an A4 page
    /// (P1.8 plan, ruling 1).
    /// </summary>
    public const double LineHeight = 1.2;

    /// <summary>
    /// German prose, 73 words, with U+202F in "z. B.", U+00A0 before both euro signs, and a soft hyphen in
    /// "Kundenbetreuung" and in "Kundennummer". At 220 points a line ends at the second.
    /// </summary>
    public const string Prose =
        "Vielen Dank f\u00FCr Ihren Auftrag. Die Rechnung f\u00FCr den Monat November enth\u00E4lt alle Leistungen, " +
        "z.\u202FB. die Wartung der Server und die Kunden\u00ADbetreuung. Bitte \u00FCberweisen Sie den Gesamtbetrag von " +
        "1.234,56\u00A0\u20AC bis zum 30. Dezember auf das angegebene Konto. F\u00FCr versp\u00E4tete Zahlungen berechnen wir eine " +
        "Mahngeb\u00FChr von 20\u00A0\u20AC. Bei Fragen erreichen Sie unsere Buchhaltung jederzeit \u00FCber die E-Mail-Adresse auf " +
        "der R\u00FCckseite oder telefonisch werktags unter der Kunden\u00ADnummer auf dem Deckblatt. Mit freundlichen " +
        "Gr\u00FC\u00DFen aus K\u00F6ln";

    /// <summary>A German IBAN, one segment, too wide for a line of 70 points.</summary>
    public const string LongWord = "DE89370400440532013000";

    /// <summary>Cologne and Munich in German, with each umlaut decomposed: an o or u, then U+0308.</summary>
    public const string Decomposed = "Ko\u0308ln, Mu\u0308nchen";

    /// <summary>
    /// The mixed paragraph's 13 spans: bold, italic, a 14 pt span, underline, strikethrough, letter spacing of 0.5
    /// points, a line feed, then Turkish, Russian and Greek sentences. Each is Regular at 10 points unless it says
    /// otherwise. At 220 points the underline ends one line, before a hanging space, and goes on at the next. The
    /// letter-spaced span holds its following space, so a narrower fill hangs a letter-spaced space.
    /// </summary>
    public static IReadOnlyList<TextSpan> MixedSpans(TrueTypeFont regular, TrueTypeFont bold, TrueTypeFont italic)
    {
        TextStyle Style(TrueTypeFont font, double size = 10, double letterSpacing = 0, bool underline = false, bool strikethrough = false) =>
            new(font, size, LineHeight, letterSpacing, underline, strikethrough);

        return
        [
            new("Zahlbar ohne Abzug innerhalb von 14 Tagen. ", Style(regular)),
            new("Wichtig: ", Style(bold)),
            new("Bitte geben Sie die Rechnungsnummer an. ", Style(italic)),
            new("Der Gesamtbetrag von ", Style(regular)),
            new("1.234,56\u00A0\u20AC", Style(regular, size: 14)),
            new(" ", Style(regular)),
            new("ist bis zum 30. Dezember", Style(regular, underline: true)),
            new(" f\u00E4llig, ", Style(regular)),
            new("nicht bis zum 15.", Style(regular, strikethrough: true)),
            new(" Der Vermerk ", Style(regular)),
            new("BEZAHLT ", Style(regular, letterSpacing: 0.5)),
            new("folgt.\n", Style(regular)),
            new(
                "Faturan\u0131z \u0130stanbul \u015Fubemize \u00F6denebilir. \u0421\u0447\u0451\u0442 \u043D\u0435\u043E\u0431\u0445\u043E\u0434\u0438\u043C\u043E \u043E\u043F\u043B\u0430\u0442\u0438\u0442\u044C \u0434\u043E 30 \u0434\u0435\u043A\u0430\u0431\u0440\u044F. " +
                "\u03A4\u03BF \u03C4\u03B9\u03BC\u03BF\u03BB\u03CC\u03B3\u03B9\u03BF \u03B5\u03BE\u03BF\u03C6\u03BB\u03B5\u03AF\u03C4\u03B1\u03B9 \u03AD\u03C9\u03C2 \u03C4\u03B9\u03C2 30 \u0394\u03B5\u03BA\u03B5\u03BC\u03B2\u03C1\u03AF\u03BF\u03C5.",
                Style(regular)),
        ];
    }
}
