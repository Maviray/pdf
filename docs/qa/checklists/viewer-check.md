# Viewer check

Status: 2026-11-24

A check by hand that the gate files open cleanly and read correctly in the viewers CI can't run. It is repeated for each release (design document, Testing and validation).
- **The hello-world file** is the P0 gate.
- **The fonts file**, the P1 fonts gate (fonts spec F-15), joins the 0.1.0 release check. It can be checked as soon as the fonts work merges, but it isn't a merge condition.
- **The text file**, the P1 text gate (text spec T-15), joins the 0.1.0 release check the same way.
- **The invoice file**, the P1 layout gate (layout spec L-17), joins it too.

This checklist is never ticked. For each run, copy the report template at the end to `docs/qa/reports/YYYY-MM-DD-viewer-check-run.md`, record the verdicts there, and commit it.

## The files

1. Open the latest successful *Build* run of the pull request being checked, on the repository's **Actions** tab. *Build* runs only for pull requests.
2. Download the `generated-pdfs` artifact and unzip it. Artifacts expire after 14 days. If it's gone, re-run the job.
3. Open the files under `Maviray.Pdf.Tests/bin/Release/net10.0/generated/`. If a path differs, search the unzipped folder for the file name.
   - The hello-world file: `HelloWorldTests/HelloWorldPassesEveryValidator.pdf`.
   - The fonts file: `FontsGateTests/FontsGatePassesEveryValidator.pdf`. Its expected text, `FontsGatePassesEveryValidator.txt`, is beside it.
   - The text file: `TextGateTests/TextGatePassesEveryCheck.pdf`. Its expected text, `TextGatePassesEveryCheck.txt`, is beside it.
   - The invoice file: `InvoiceGateTests/InvoiceGatePassesEveryCheck.pdf`. The strings its first page must copy out, `InvoiceGatePassesEveryCheck.txt`, are beside it.
4. Note each PDF's SHA-256 for the report: `Get-FileHash <path> -Algorithm SHA256` in PowerShell. The writer's output is deterministic, so a later commit that produces the same hash is covered by the same verdicts.

The other files in the artifact belong to other tests. Some are broken on purpose, to prove that the validators catch errors, so don't use them for this check.

## What the hello-world page should look like

The page is A4 in portrait (210 × 297 mm) and white. It holds exactly these four things, and nothing else:

| Element | What it looks like | Where |
| --- | --- | --- |
| `Hello, world` | Large sans-serif text (Helvetica 24 pt, or the viewer's stand-in for Helvetica) | Left edge 2.5 cm from the page's left edge; baseline about 3.2 cm from the top |
| `Grüße aus Köln – 25 €` | The same font, smaller (14 pt) | Directly below, same left edge; baseline about 4.3 cm from the top |
| Blue rectangle | Solid blue, about 7.1 × 3.5 cm: twice as wide as tall | Left edge in line with the text; top about 6.4 cm from the page's top |
| Red rectangle | The same size, as a thick red outline (about 1.4 mm) with a white inside | To the right of the blue one, with about 1 cm between them, and level with it |

## What the fonts page should look like

The page is A4 in portrait and white.
- **Text:** 24 lines of Noto Sans at 14 pt, and nothing else.
- **Position:** each line starts 2.5 cm from the page's left edge. The first baseline is about 1.8 cm from the top, and the last about 22.6 cm from the top.
- **Blocks:** the lines form four blocks of six, in regular, bold, italic and bold italic, from the top. A small gap separates the blocks.
- **Lines in each block:**

  | Language | Line |
  | --- | --- |
  | German | `Grüße aus Köln: Äpfel, Öl und Übermaß` |
  | Turkish | `Iğdır'da ılık bir gün; İstanbul, Şişli` |
  | Azerbaijani | `Əli Gəncədə yaşayır` |
  | Russian | `Счёт № 42 от 20 ноября` |
  | Ukrainian | `Ґанок, їжак і євро` |
  | Greek | `Τιμολόγιο για την Αθήνα` |

- **What to look out for:**
  - No character shows as an empty box or a question mark.
  - Every accent sits on its own letter: the dot of `İ`, `ı` with no dot, the breve of `ğ`, the cedilla of `Ş`, the upturn of `Ґ`, the two dots of `ї` and `ё`, and the Greek tonos.
  - The schwa `Ə` and `ə` show as letters of their own, not as an `e` turned around or a box.
  - The italic blocks lean, and the bold blocks are heavier.

## What the text page should look like

The page is A4 in portrait and white.
- **Text:** six paragraphs of Noto Sans at 10 pt, one below the other, and nothing else. Every paragraph's left edge is 2.5 cm from the page's left edge.
- **Position:** the first paragraph starts about 1.3 cm from the top, and the last ends about 28.3 cm from the top. A small gap separates the paragraphs.
- **The paragraphs, from the top:**

  | Paragraph | What it looks like |
  | --- | --- |
  | 1 | German prose about an invoice, 13 lines, ragged on the right, in a column about 7.8 cm wide |
  | 2 | The same prose in 18 lines, each centred in a column about 5.6 cm wide |
  | 3 | The same 18 lines, each flush with the right edge of that column, about 8.2 cm from the page's left edge |
  | 4 | Five lines in German, with a bold `Wichtig:`, an italic sentence, a larger `1.234,56 €`, an underlined `ist bis zum 30. Dezember`, a struck-through `nicht bis zum 15.` and a spaced-out `BEZAHLT`. After `folgt.` a new line starts, and a Turkish, a Russian and a Greek sentence run on across the last three lines |
  | 5 | `DE893704004` and, on the next line, `40532013000` |
  | 6 | `Köln, München` |

- **What to look out for:**
  - Line 11 of the first paragraph ends with `Kunden-`, and the next line starts with `nummer`. No hyphen shows inside `Kundenbetreuung`.
  - `z. B.`, `1.234,56 €` and `20 €` never break across two lines.
  - In the fourth paragraph, the underline runs under `ist` at the end of the third line, but not under the space after it, and goes on under `bis zum 30. Dezember` at the start of the fourth. The strike runs through `nicht bis zum 15.` at the end of the fourth line.
  - No character shows as an empty box or a question mark, and every accent sits on its own letter: the dots of `İ` and `ö`, `ı` with no dot, the cedilla of `ş`, the two dots of `ё`, and the Greek tonos.

## What the invoice should look like

Ten A4 pages in portrait, white, in Noto Sans. Nothing reaches into the 2 cm margin on any side.
- **The header, on every page:**
  - `Maviray GmbH`, large and bold, on the left;
  - `Rechnung RE-2026-0042` in bold on the right, with `Rechnungsdatum: 24.11.2026` below it;
  - the column headings `Pos.`, `Beschreibung`, `Menge`, `Einzelpreis`, `USt.` and `Betrag`, in bold, over a thin black rule. The last four are flush right.
- **Page 1, below the header:**
  - the two addresses side by side, each under a small grey label: `Absender`, Maviray GmbH in Köln, on the left, and `Rechnungsempfänger`, Yıldız Lojistik GmbH in Düsseldorf, on the right;
  - then items 1 to 35.
- **The items,** one line each:
  - the position, and the description, which ends with `Los` and the position in three digits;
  - then, flush right, the quantity with its unit, the unit price, the VAT rate (`19 %` or `7 %`) and the amount.
  - Pages 2 to 9 hold 40 items each, and page 10 holds items 356 to 385.
- **Page 10, after the items:** a black rule, then the totals, flush right:
  - `Nettobetrag` `122.279,80 €`;
  - `Umsatzsteuer 19 %` `22.291,39 €`;
  - `Umsatzsteuer 7 %` `346,97 €`;
  - `Gesamtbetrag` `144.918,16 €`, in bold.
  - The rest of the page is empty down to the footer.
- **The footer, on every page:** a thin grey rule, the bank details and the company registration in small grey type, and `Seite n von 10` centred below them.
- **What to look out for:**
  - No item is missing or repeated where a page ends: the last position on one page and the first on the next are consecutive.
  - No character shows as an empty box or a question mark.
  - Every accent sits on its own letter:
    - the dot of `İ` in `İnönü`, and `ı` with no dot in `Yıldız` and `Kılıç`;
    - the cedillas of `ş` and `ç`, and the breve of `ğ` in `Iğdır`;
    - the two dots of `ё` in `Ковалёва` and `Соловьёв`, and the breve of `й` in `Юрий`.
  - The guillemets « and » stand around `Сети и связь`.

## Viewers

| Viewer | Engine | Make sure that |
| --- | --- | --- |
| Adobe Acrobat Reader | Adobe's own | — |
| Google Chrome | PDFium | the file opens in Chrome's own viewer: drag it into a tab, or press Ctrl+O. Chrome mustn't download it or hand it to an Acrobat extension. |
| Mozilla Firefox | pdf.js | the file opens in Firefox's own viewer. Check that *Settings › Applications › Portable Document Format* is "Open in Firefox". |
| macOS Preview | Apple PDFKit | — |

## Checks, for each viewer and each file

1. **Opens cleanly.** The file opens with no error, warning or "repaired" message.
2. **Looks right.**
   - The page matches its description above. For the hello-world file, that's the four elements, their sizes, positions and colours, and nothing else. For the fonts file, it's the 24 lines in their styles, with every character and accent drawn. For the text file, it's the six paragraphs, their alignment and their decorations. For the invoice, it's the ten pages, with the header and footer on each and the totals on the last.
   - Check the page size in the viewer's document properties:
     - Acrobat: *File › Properties › Page Size*;
     - Chrome: *⋮ › Document properties*;
     - Firefox: *» › Document Properties*.

     The size is A4, 210 × 297 mm (8.27 × 11.69 in), portrait.
3. **Text copies out exactly.**
   - Select all the text on the page, copy it, then run the file's snippet below in PowerShell. Both compare character by character (ordinal), so a lookalike or a decomposed accent, such as `o` followed by U+0308 instead of `ö`, counts as a difference. PowerShell's own `-ceq` and `Compare-Object` treat those as equal.
   - For the hello-world file, the snippet holds the two lines:

     ```powershell
     $expected = 'Hello, world', "Gr$([char]0xFC)$([char]0xDF)e aus K$([char]0xF6)ln $([char]0x2013) 25 $([char]0x20AC)"
     $actual = @(Get-Clipboard | ForEach-Object TrimEnd | Where-Object { $_ })
     if ([string]::Equals(($actual -join "`n"), ($expected -join "`n"), [StringComparison]::Ordinal)) { 'Exact' } else { $actual | ForEach-Object { ($_.ToCharArray() | ForEach-Object { 'U+{0:X4}' -f [int]$_ }) -join ' ' } }
     ```

     It prints `Exact` when the text matches. Otherwise it prints each copied line as character codes, so a hyphen (U+002D) instead of the en dash (U+2013), or a no-break space, shows up.
   - For the fonts file and the text file, the expected lines come from the file's `.txt`, so no character needs typing. Put the path of that `.txt` in the first line: `FontsGatePassesEveryValidator.txt` for the fonts file, and `TextGatePassesEveryCheck.txt` for the text file:

     ```powershell
     $expected = @(Get-Content -Encoding UTF8 '<path>\<name>.txt')
     $actual = @(Get-Clipboard | ForEach-Object TrimEnd | Where-Object { $_ })
     if ([string]::Equals(($actual -join "`n"), ($expected -join "`n"), [StringComparison]::Ordinal)) { 'Exact' }
     else { 0..([Math]::Max($expected.Count, $actual.Count) - 1) | Where-Object { -not [string]::Equals($expected[$_], $actual[$_], [StringComparison]::Ordinal) } | ForEach-Object { "Line $($_ + 1): $($actual[$_])"; (([string]$actual[$_]).ToCharArray() | ForEach-Object { 'U+{0:X4}' -f [int]$_ }) -join ' ' } }
     ```

     It prints `Exact` when every line matches, in order: 24 for the fonts file, 60 for the text file. Otherwise, for each line that differs, it prints the line number, the copied line and its character codes. A missing line shows as an empty copied line.
   - The text file holds characters that viewers may paste differently: the no-break spaces U+00A0 and U+202F, and the U+00AD at the end of line 11. A viewer may paste such a space as a space (U+0020), or drop the U+00AD or paste it as a hyphen (U+002D). A viewer that drops it may also join line 11 to line 12, so every later line shows as different, one line on. Such a difference is recorded in the notes, and it doesn't fail the file. Any other difference does.
   - For the invoice file, select the text of page 1, or all the text, and copy it. Viewers paste the cells of a row in different orders and with different separators, so this snippet doesn't compare lines. It checks that each line of `InvoiceGatePassesEveryCheck.txt` occurs somewhere in the copied text, exactly. Put that file's path in the first line:

     ```powershell
     $expected = @(Get-Content -Encoding UTF8 '<path>\InvoiceGatePassesEveryCheck.txt')
     $actual = (Get-Clipboard) -join "`n"
     $missing = @($expected | Where-Object { $actual.IndexOf($_, [StringComparison]::Ordinal) -lt 0 })
     if ($missing.Count -eq 0) { 'Exact' } else { $missing | ForEach-Object { "Missing: $_"; ($_.ToCharArray() | ForEach-Object { 'U+{0:X4}' -f [int]$_ }) -join ' ' } }
     ```

     It prints `Exact` when all 18 strings occur. Otherwise it prints each missing string with its character codes.
   - Line breaks, blank lines and spaces at the end of a line don't count, because viewers paste them differently. Every character within each line must match.
4. **Acrobat only: no repair.** Close the file. Acrobat must not ask whether to save changes, because asking means it repaired the file.

A viewer passes when all its checks pass. A viewer that can't be checked, such as Preview without a Mac, is recorded as "not checked", with the reason.

## Report template

```markdown
# Viewer check run, YYYY-MM-DD

Checklist: [viewer-check.md](../checklists/viewer-check.md). Files from the `generated-pdfs` artifact of CI run
<run link>, commit <sha>. Checked by <name> on <OS and version>.

## Hello-world file

`HelloWorldTests/HelloWorldPassesEveryValidator.pdf`, SHA-256 `<hash>`.

| Viewer | Version | Opens cleanly | Looks right | Text copies exactly | No repair (Acrobat) | Verdict |
| --- | --- | --- | --- | --- | --- | --- |
| Adobe Acrobat Reader | | | | | | |
| Google Chrome | | | | | n/a | |
| Mozilla Firefox | | | | | n/a | |
| macOS Preview | | | | | n/a | |

## Fonts file

`FontsGateTests/FontsGatePassesEveryValidator.pdf`, SHA-256 `<hash>`.

| Viewer | Version | Opens cleanly | Looks right | Text copies exactly | No repair (Acrobat) | Verdict |
| --- | --- | --- | --- | --- | --- | --- |
| Adobe Acrobat Reader | | | | | | |
| Google Chrome | | | | | n/a | |
| Mozilla Firefox | | | | | n/a | |
| macOS Preview | | | | | n/a | |

## Text file

`TextGateTests/TextGatePassesEveryCheck.pdf`, SHA-256 `<hash>`.

| Viewer | Version | Opens cleanly | Looks right | Text copies exactly | No repair (Acrobat) | Verdict |
| --- | --- | --- | --- | --- | --- | --- |
| Adobe Acrobat Reader | | | | | | |
| Google Chrome | | | | | n/a | |
| Mozilla Firefox | | | | | n/a | |
| macOS Preview | | | | | n/a | |

## Invoice file

`InvoiceGateTests/InvoiceGatePassesEveryCheck.pdf`, SHA-256 `<hash>`.

| Viewer | Version | Opens cleanly | Looks right | Text copies exactly | No repair (Acrobat) | Verdict |
| --- | --- | --- | --- | --- | --- | --- |
| Adobe Acrobat Reader | | | | | | |
| Google Chrome | | | | | n/a | |
| Mozilla Firefox | | | | | n/a | |
| macOS Preview | | | | | n/a | |

**Gate:** pass or fail, for each file. A file passes when Acrobat Reader, Chrome and Firefox all pass it.

Notes: <anything a viewer did differently, with screenshots linked if useful>
```
