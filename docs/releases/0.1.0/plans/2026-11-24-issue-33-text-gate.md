# P1.8 Text gate: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The P1 text gate of T-15. One A4 page carries six paragraphs laid out by the text engine, and a test checks it:
- every line is a correct greedy line, by a check that doesn't use the filler;
- the lines' text is as written out in the test;
- pdftotext reads the lines back;
- the render has ink in every line's box and nowhere else;
- qpdf accepts the file, and pdffonts reports three embedded, subset, Unicode-mapped fonts;
- writing it twice gives the same bytes.

The viewer checklist describes the file, for the check by hand.

**Architecture:**
- **The greedy check** is a test helper, `GreedyLineCheck`. It rebuilds the paragraph's text from its spans with `TextNormalizer`. It then finds UAX #14 opportunities with `LineBreaker` and clusters with `StringInfo`, and measures each line from the fonts' advances by T-07, T-09 and T-10.
- **Its own tests** feed it wrong layouts and expect each wrong line to be reported.
- **The gate test** paints the six layouts with P1.7's `LayoutPainter`, and runs every check.

**Tech Stack:** .NET 10, C#, xUnit v3. The validators are qpdf and poppler-utils: from apt in CI, and locally qpdf 12.4.2 and poppler 26.09.0 from winget. The user allowed installing test tools on 2026-11-24.

**Spec:** [P1 Text](../specs/2026-11-23-p1-text-design.md), decision T-15, with T-07 to T-11. Work item [#33](https://github.com/Maviray/pdf/issues/33). The branch `feature/33-text-gate` is stacked on `feature/32-text-layout` (PR #42), which is stacked on #41.

## Global Constraints

- This item changes no code in `src/`. T-15: "A test helper draws the layouts with `ShowGlyphs` and its `TJ` form, and fills the decorations as rectangles."
- The gate is in the namespace `Maviray.Pdf.Tests.Text`, so CI's run without ICU runs it too. The sources under `tests/Maviray.Pdf.Tests/Text/` stay ASCII (`SourceRulesTests.TextTestsSpellNonAsciiAsEscapes`), with every other character written as a `\u` escape.
- **The page:**
  - A4 (595.2756 × 841.8898 points), compressed with `CompressionLevel.Optimal`;
  - written with `PdfDocumentWriterTests.Write`, so the file ID is fixed;
  - saved with `GeneratedFiles.Save`, so CI uploads it.
- Noto Sans Regular, Bold and Italic, from `TestFonts.NotoSans(style)`. Every style is 10 pt with a line height of 1.2, except the one 14 pt span.
- Each task ends with one commit, subject `#33 <summary>`. The records are the last commit.

## Rulings made while planning

A throwaway probe laid out the draft gate text, painted it, and read it back with poppler 26.09.0. It found four things the spec doesn't settle:

1. **The paragraphs don't fit on an A4 page at the normal line height.** At 1.362 they need 864 points, before any gap between paragraphs.
   - **Ruling:** every style sets a line height of 1.2, and the prose has 73 words, within T-15's "about 80". The page then needs 765 points with its gaps.
   - **Why:** T-15 doesn't fix the line height. A set line height also exercises T-08's negative leading on a real page.
   - **Cost if wrong:** the gate doesn't show the normal line height, which P1.7's tests pin for Noto Sans.
2. **pdftotext's default mode joins a line that ends with a hyphen to the next, and drops the hyphen.** It returned `Kundennummer` for `Kunden` U+00AD / `nummer`, and `EMail-Adresse` for `E-` / `Mail-Adresse`.
   - T-15 wants the soft hyphen's line to end with U+00AD. `-raw` keeps the hyphens but drops the narrow no-break space (`z.B.`). `-layout` keeps all three, with each line indented by its x position.
   - **Ruling:** the gate reads the text with `pdftotext -layout`, and trims each line at both ends.
   - **Cost if wrong:** leading spaces become a blind spot, which the ink check covers, because it finds each line at its offset.
3. **pdftotext writes U+00A0 and U+202F as U+0020, in every mode.** The ToUnicode map is right: qpdf's QDF form shows `<0030> <00A0>` and `<0023> <202F>`. So the spaces come from poppler, which treats any whitespace character as a word break.
   - **Ruling:** the pdftotext comparison maps U+00A0 and U+202F to U+0020 in the expected lines. The lines' text, which the engine copies out, is compared exactly, and so is the clipboard in the viewer check.
4. **The Windows build of poppler writes `Syntax Error: No display font for 'Symbol'` and `... for 'ArialUnicode'` to stderr** for any file with a font that isn't embedded. The poppler helper fails on anything on stderr, so the P0 Helvetica tests fail locally. The message is about the machine's substitute fonts, not the file, and Ubuntu's build doesn't write it.
   - **Ruling:** the helper ignores exactly that message (Task 1).
   - **Cost if wrong:** none in CI, where the message never appears.

## Review Focus

1. **A check that passes vacuously.** The greedy check finds nothing wrong because it measures the same way the filler does, or never compares. Its own tests (Task 2) feed it four kinds of wrong layout, each made by the real engine at another width or alignment. The check must not use `PreparedParagraph`'s widths, `BreakOpportunities` or `Fill`.
2. **Letter spacing and decorations in the independent width.** The spacing comes after each drawn cluster except the line's last, in that cluster's own style, and never after hanging spaces. The mixed paragraph's letter-spaced `BEZAHLT `, with its space, puts this on the page, and at a line's end in the check's narrower fills.
3. **The soft hyphen's line.** At 220 points the line `telefonisch werktags unter der Kunden` U+00AD fits with its hyphen. pdftotext must return it ending with U+00AD, and the mid-line U+00AD in `Kundenbetreuung` must draw nothing.
4. **No-break spaces.** `z.` U+202F `B.`, `1.234,56` U+00A0 `€` and `20` U+00A0 `€` stay on one line at every width of the gate. The greedy check confirms there's no opportunity inside them.
5. **Ink outside the lines.** Under the negative leading of 1.2, glyphs must stay inside their line's box with 2 points of margin, and the underline must too. The gaps between paragraphs must stay white.

---

### Task 1: The Windows poppler build's font message

**Files:**
- Modify: `tests/Maviray.Pdf.Tests/Validation/Poppler.cs`
- Test: `tests/Maviray.Pdf.Tests/Validation/PopplerDiagnosticsTests.cs`

**Interfaces:**
- Produces: `internal static IReadOnlyList<string> Poppler.Diagnostics(string standardError)`, the lines of poppler's stderr that report a problem with the file. `Render` and `ReadFonts` fail when it isn't empty, instead of when stderr isn't empty.

- [ ] **Step 1: Test.** In `PopplerDiagnosticsTests`:
  - `TheWindowsBuildsMissingDisplayFontsAreNoProblem`: `Diagnostics("Syntax Error: No display font for 'Symbol'\nSyntax Error: No display font for 'ArialUnicode'\n")` is empty, also with CRLF line ends;
  - `EveryOtherMessageIsAProblem`: `Diagnostics("Syntax Error: No display font for 'Symbol'\nSyntax Error (1234): Illegal character '>'\n")` is exactly `["Syntax Error (1234): Illegal character '>'"]`;
  - `NoOutputIsNoProblem`: `Diagnostics("")` is empty.
- [ ] **Step 2:** Run them. They fail to compile, because `Diagnostics` doesn't exist.
- [ ] **Step 3:** Implement `Diagnostics`.
  - It splits on LF, trims a CR at each line's end, and drops empty lines.
  - It drops each line that matches `^Syntax Error: No display font for '[^']*'$` exactly.
  - `Render` and `ReadFonts` use it, and their failure messages list the lines that remain.
- [ ] **Step 4:** Run the three tests, then the suite with `MAVIRAY_PDF_REQUIRE_VALIDATORS=true`. `HelloWorldPassesEveryValidator` and `PdffontsReportsHelveticaAsNotEmbedded`, which failed locally before, now pass. Commit `#33 let the Windows build of poppler run the checks`.

### Task 2: The greedy check

**Files:**
- Create: `tests/Maviray.Pdf.Tests/Text/GreedyLineCheck.cs`
- Create: `tests/Maviray.Pdf.Tests/Text/TextGateText.cs`
- Test: `tests/Maviray.Pdf.Tests/Text/GreedyLineCheckTests.cs`

**Interfaces:**
- Consumes: `TextLayout`, `TextLine`, `Paragraph`, `TextSpan` and `TextStyle` from P1.7, and `TextNormalizer.ToNfc` and `LineBreaker.FindBreaks` from P1.5 and P1.6.
- Produces:
  - `TextGateText`, an `internal static` class with the gate's strings from Task 3's tables: `Prose`, `LongWord`, `Decomposed`, `LineHeight` (1.2), and `MixedSpans(TrueTypeFont regular, TrueTypeFont bold, TrueTypeFont italic)`, which returns the 13 spans.
  - `internal static IReadOnlyList<string> GreedyLineCheck.Problems(TextLayout layout, double width)`. Each problem names the line's index, its text and what is wrong. The list is empty when every line is a correct greedy line of `layout.Source.Source` at `width`.

**What it checks**, from the spans of `layout.Source.Source`, without `PreparedParagraph`'s widths, `BreakOpportunities` or `Fill`:
1. **The text.** The spans' texts, each passed through `TextNormalizer.ToNfc` and then joined, equal `layout.Source.Text`. Each UTF-16 index belongs to the span it came from.
2. **The cover.**
   - The lines are contiguous: the first starts at 0, and each starts where the one before ends.
   - The last line ends at the text's end.
   - No line holds a required break of `LineBreaker.FindBreaks` before its end.
3. **The break.** A line's end is a cluster boundary (`StringInfo.GetNextTextElementLength`). It is an Allowed or Required boundary of `FindBreaks`, or else a cut.
   - A cut may happen only when the line's first segment, from its start to the first opportunity after it, doesn't fit.
   - The cut must be inside that segment.
4. **The width**, measured from `TrueTypeFont.GetGlyphId` and `GetAdvanceWidth` × size ÷ `UnitsPerEm`, with each code point in its own span's style:
   - **Not measured:** trailing U+0020, U+0009 and break characters (U+000A to U+000D, U+0085, U+2028 and U+2029), and a U+00AD that isn't at the line's end.
   - **A U+00AD at the line's end, with a drawn cluster before it and text after it,** is the glyph of U+2010 (or else U+002D), and counts as a cluster.
   - **Any other code point the font doesn't map** is a problem; the gate's text has none.
   - **Letter spacing:** each cluster's letter spacing, in the style of the span holding its first code unit, follows every drawn cluster except the line's last.
   - The measured width must equal `line.Width` within 10⁻⁹.
5. **The fit.**
   - The width is at most `width` + 10⁻⁶, unless the line is a single cluster.
   - Unless the line ends at a required break or the text's end, the line extended to the next opportunity after its end (for a cut, by one more cluster) doesn't fit.
6. **The offset.** `line.Offset` equals 0, (W − w) ÷ 2 or W − w, by the paragraph's alignment, within 10⁻⁹, where w is the measured width. It is 0 when w > W or W is infinite.

- [ ] **Step 1: Test.** In `GreedyLineCheckTests`, on `TextGateText.Prose` in Noto Sans Regular at 10 pt with `TextGateText.LineHeight`, and on `TextGateText.MixedSpans`:
  - `CorrectFillsHaveNoProblems`: the prose and the mixed paragraph, filled at 60, 100, 160 and 220, and at infinity, in all three alignments, give no problems;
  - `ALineThatCouldHoldTheNextSegmentIsReported`: the prose filled at 200 and checked at 220 reports at least one line whose next segment fits;
  - `ALineThatDoesntFitIsReported`: the prose filled at 240 and checked at 220 reports at least one line that doesn't fit;
  - `ACutWhereASegmentFitsIsReported`: `Rechnungsnummer an` filled at 40 and checked at 200 reports that line 0's end is a cut where a segment fits;
  - `AnOffsetThatDoesntMatchTheAlignmentIsReported`: the prose filled right-aligned at 160, wrapped with `new TextLayout(leftPrepared, …, rightLines)` so the paragraph is left-aligned, reports a wrong offset on every line whose offset isn't 0.
- [ ] **Step 2:** Run them. They fail to compile, because `GreedyLineCheck` doesn't exist.
- [ ] **Step 3:** Write `TextGateText` with Task 3's strings, every character beyond ASCII as an escape, and implement `GreedyLineCheck.Problems` as above.
- [ ] **Step 4:** Run the tests. The four that expect problems must find the one they name, and the correct fills must find none.
  - Then a mutant: make the check measure letter spacing after the line's last cluster as well. `CorrectFillsHaveNoProblems` must fail on the mixed paragraph. Restore the check.
- [ ] **Step 5:** Verify the whole suite passes. Commit `#33 add a greedy-line check that doesn't use the filler`.

### Task 3: The gate test

**Files:**
- Modify: `tests/Maviray.Pdf.Tests/Validation/Poppler.cs`
- Create: `tests/Maviray.Pdf.Tests/Text/TextGateTests.cs`

**Interfaces:**
- Consumes: `TextGateText`, `GreedyLineCheck.Problems`, `LayoutPainter.Paint(page, layout, fonts, left, top)`, `Poppler.Render`, `Poppler.ListFonts`, `Qpdf.Check` and `GeneratedFiles`.
- Produces:
  - `Poppler.ExtractText(string path, bool physicalLayout = false)`, which passes `-layout` when asked. Blind spot, documented on it: in that mode each line is indented by its x position.

**The page.** Six paragraphs, each with its left edge at x = 72 and stacked downward from y = 36, with 8 points between paragraphs:

| # | Paragraph | Width | Alignment | Top |
| --- | --- | --- | --- | --- |
| 1 | The prose, Regular | 220 | Left | 36 |
| 2 | The prose, Regular | 160 | Center | 200 |
| 3 | The prose, Regular | 160 | Right | 424 |
| 4 | The mixed spans | 220 | Left | 648 |
| 5 | `DE89370400440532013000`, Regular | 70 | Left | 756.8 |
| 6 | `Ko` U+0308 `ln, Mu` U+0308 `nchen`, Regular | 220 | Left | 788.8 |

The last paragraph ends 800.8 points from the top.

**The prose**, 73 words, with U+202F in `z. B.`, U+00A0 before both `€`, and U+00AD in `Kunden|betreuung` and `Kunden|nummer`:

> Vielen Dank für Ihren Auftrag. Die Rechnung für den Monat November enthält alle Leistungen, z. B. die Wartung der Server und die Kunden|betreuung. Bitte überweisen Sie den Gesamtbetrag von 1.234,56 € bis zum 30. Dezember auf das angegebene Konto. Für verspätete Zahlungen berechnen wir eine Mahngebühr von 20 €. Bei Fragen erreichen Sie unsere Buchhaltung jederzeit über die E-Mail-Adresse auf der Rückseite oder telefonisch werktags unter der Kunden|nummer auf dem Deckblatt. Mit freundlichen Grüßen aus Köln

**The mixed spans**, in order. Each is Regular 10 pt unless the style says otherwise:

| Text | Style |
| --- | --- |
| `Zahlbar ohne Abzug innerhalb von 14 Tagen. ` | |
| `Wichtig: ` | Bold |
| `Bitte geben Sie die Rechnungsnummer an. ` | Italic |
| `Der Gesamtbetrag von ` | |
| `1.234,56` U+00A0 `€` | 14 pt |
| ` ` | |
| `ist bis zum 30. Dezember` | underlined |
| ` fällig, ` | |
| `nicht bis zum 15.` | struck through |
| ` Der Vermerk ` | |
| `BEZAHLT ` | letter spacing 0.5 |
| `folgt.` LF | |
| `Faturanız İstanbul şubemize ödenebilir. Счёт необходимо оплатить до 30 декабря. Το τιμολόγιο εξοφλείται έως τις 30 Δεκεμβρίου.` | |

**The expected lines**, 60 in all, as the engine copies them out. `·` marks U+00A0, `⍽` U+202F, and `‐` at a line's end the hyphen that copies out as U+00AD:

1. Left at 220: `Vielen Dank für Ihren Auftrag. Die Rechnung` / `für den Monat November enthält alle` / `Leistungen, z.⍽B. die Wartung der Server und` / `die Kundenbetreuung. Bitte überweisen Sie` / `den Gesamtbetrag von 1.234,56·€ bis zum 30.` / `Dezember auf das angegebene Konto. Für` / `verspätete Zahlungen berechnen wir eine` / `Mahngebühr von 20·€. Bei Fragen erreichen` / `Sie unsere Buchhaltung jederzeit über die E-` / `Mail-Adresse auf der Rückseite oder` / `telefonisch werktags unter der Kunden‐` / `nummer auf dem Deckblatt. Mit freundlichen` / `Grüßen aus Köln`
2. and 3. Centred and right-aligned at 160, the same 18 lines each: `Vielen Dank für Ihren Auftrag. Die` / `Rechnung für den Monat` / `November enthält alle` / `Leistungen, z.⍽B. die Wartung der` / `Server und die Kundenbetreuung.` / `Bitte überweisen Sie den` / `Gesamtbetrag von 1.234,56·€ bis` / `zum 30. Dezember auf das` / `angegebene Konto. Für` / `verspätete Zahlungen berechnen` / `wir eine Mahngebühr von 20·€.` / `Bei Fragen erreichen Sie unsere` / `Buchhaltung jederzeit über die E-` / `Mail-Adresse auf der Rückseite` / `oder telefonisch werktags unter` / `der Kundennummer auf dem` / `Deckblatt. Mit freundlichen` / `Grüßen aus Köln`
4. Mixed at 220: `Zahlbar ohne Abzug innerhalb von 14 Tagen.` / `Wichtig: Bitte geben Sie die Rechnungsnummer` / `an. Der Gesamtbetrag von 1.234,56·€ ist` / `bis zum 30. Dezember fällig, nicht bis zum 15.` / `Der Vermerk BEZAHLT folgt.` / `Faturanız İstanbul şubemize ödenebilir. Счёт` / `необходимо оплатить до 30 декабря. Το` / `τιμολόγιο εξοφλείται έως τις 30 Δεκεμβρίου.`
5. At 70: `DE893704004` / `40532013000`
6. `Köln, München`

- [ ] **Step 1: Test.** `TextGateTests.TextGatePassesEveryCheck` prepares and fills the six paragraphs, writes the page with `LayoutPainter` and saves it. It also saves the 60 expected lines, joined with LF plus a final LF, as UTF-8, with `GeneratedFiles.SaveAs(…, ".txt")`. Then:
  1. No paragraph reports a missing glyph.
  2. `GreedyLineCheck.Problems(layout, width)` is empty for each paragraph.
  3. Each line's text, its runs' code points in order, equals its expected line, compared ordinally. The mixed paragraph's underline ends its third line at the line's width, so not under the hanging space, and starts its fourth at x = 0.
  4. Writing the page again gives equal bytes.
  5. `Poppler.Render(path)` gives one page, with ink in every line's box and nowhere else:
     - a box runs from 72 + offset − 3 to 72 + offset + width + 3 across, and from the line's top − 2 to its bottom + 2 down, flipped to PDF space with 841.8898 − y;
     - each box holds a pixel with a channel below 128, and every pixel outside all boxes is white;
     - the boxes together cover less than half the page.
  6. `Poppler.ExtractText(path, physicalLayout: true)` is split on LF. Each line is trimmed at both ends, and empty lines are dropped. The result equals the 60 lines, with U+00A0 and U+202F written as U+0020. This blind spot, and the one for the line ends, is written next to the comparison.
  7. `Qpdf.Check(path)`.
  8. `Poppler.ListFonts(path)` reports three fonts, each `CID TrueType`, embedded, subset and Unicode-mapped, named `^[A-Z]{6}\+` and then `NotoSans-Regular`, `NotoSans-Bold` and `NotoSans-Italic`, as a set.
- [ ] **Step 2:** Run it with `MAVIRAY_PDF_REQUIRE_VALIDATORS=true`. It passes.

  A gate test can't fail before the code exists, because P1.7 built it. Its red steps are mutants that the checks catch. Watch each fail, then restore it:
  - paragraph 1 filled at 230 but checked at 220 fails check 2;
  - two expected lines of paragraph 2 swapped fail check 3;
  - the default pdftotext mode fails check 6 on the soft hyphen's line;
  - paragraph 3 painted 20 points to the left fails check 5.
- [ ] **Step 3:** Run the text tests with `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true`; the gate passes there too. Verify the whole suite passes. Commit `#33 add the text gate`.

### Task 4: The viewer checklist

**Files:**
- Modify: `docs/qa/checklists/viewer-check.md`

- [ ] **Step 1:** Add the text file, keeping the other two as they are.
  - **The files:** `TextGateTests/TextGatePassesEveryCheck.pdf`, with its expected text `TextGatePassesEveryCheck.txt` beside it.
  - **What the text page should look like:** A4, white, six paragraphs of Noto Sans at 10 pt in one column, with their left edge 2.54 cm from the page's left edge. The first starts 1.27 cm from the top, and the last ends 28.25 cm from the top. Each paragraph's look, in order:
    - German prose, ragged right, in a column 7.76 cm wide;
    - the same, centred in a column 5.64 cm wide;
    - the same, flush right at 8.18 cm;
    - the mixed paragraph, with its bold, italic, larger, underlined, struck-through and spaced-out words, a new line, then the Turkish, Russian and Greek sentences;
    - the IBAN, broken across two lines;
    - `Köln, München`.
  - **What to look out for:**
    - In the first paragraph, line 11 ends with `Kunden-`, and no hyphen shows inside `Kundenbetreuung`.
    - `z. B.`, `1.234,56 €` and `20 €` never break across lines.
    - The underline runs under `ist` at the end of the mixed paragraph's third line, not under the space after it, and on under `bis zum 30. Dezember` on the fourth. The strike runs through `nicht bis zum 15.`.
    - Every accent sits on its letter, as in the fonts file.
  - **Text copies out exactly:** the fonts file's snippet, with this file's `.txt`. It passes when it prints `Exact`. Viewers may paste a no-break space (U+00A0, U+202F) as a space, or drop or replace the U+00AD at the end of line 11. Such a difference is recorded in the notes, and it doesn't fail the file; any other difference does.
  - **The report template:** a third table, for the text file.
  - **The gate line:** the text file joins the 0.1.0 release check, and it isn't a merge condition (T-15).
  - **The status date:** 2026-11-24.
- [ ] **Step 2:** Check the checklist against Task 3's constants:
  - the path;
  - 72 points (2.54 cm);
  - the widths: 220 points is 7.76 cm, and 72 + 160 points is 8.18 cm;
  - the first top, 36 points (1.27 cm), and the last bottom, 800.8 points (28.25 cm);
  - line 11 of paragraph 1.
- [ ] **Step 3:** Commit `#33 describe the text gate file in the viewer checklist`.

### Task 5: Records

- [ ] Write `docs/releases/0.1.0/execution-logs/2026-11-24-issue-33-text-gate.md`.
- [ ] Add #33's row to the release index, "In review", with the plan and the log.
- [ ] Commit `#33 add the P1.8 plan and execution log`.
- [ ] Push, and open the PR into `develop` with `Refs #33`. Its description says it is stacked on #42 and #41.
- [ ] Record the first CI run in the log: tests run, and 0 skipped.
