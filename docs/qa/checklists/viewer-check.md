# Viewer check

Status: 2026-11-23

A check by hand that the gate files open cleanly and read correctly in the viewers CI can't run. It is repeated for each release (design document, Testing and validation).
- **The hello-world file** is the P0 gate.
- **The fonts file**, the P1 fonts gate (fonts spec F-15), joins the 0.1.0 release check. It can be checked as soon as the fonts work merges, but it isn't a merge condition.

This checklist is never ticked. For each run, copy the report template at the end to `docs/qa/reports/YYYY-MM-DD-viewer-check-run.md`, record the verdicts there, and commit it.

## The files

1. Open the latest successful *Build* run of the pull request being checked, on the repository's **Actions** tab. *Build* runs only for pull requests.
2. Download the `generated-pdfs` artifact and unzip it. Artifacts expire after 14 days. If it's gone, re-run the job.
3. Open the files under `Maviray.Pdf.Tests/bin/Release/net10.0/generated/`. If a path differs, search the unzipped folder for the file name.
   - The hello-world file: `HelloWorldTests/HelloWorldPassesEveryValidator.pdf`.
   - The fonts file: `FontsGateTests/FontsGatePassesEveryValidator.pdf`. Its expected text, `FontsGatePassesEveryValidator.txt`, is beside it.
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
   - The page matches its description above. For the hello-world file, that's the four elements, their sizes, positions and colours, and nothing else. For the fonts file, it's the 24 lines in their styles, with every character and accent drawn.
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
   - For the fonts file, the expected lines come from its `.txt` file, so no character needs typing. Put that file's path in the first line:

     ```powershell
     $expected = @(Get-Content -Encoding UTF8 '<path>\FontsGatePassesEveryValidator.txt')
     $actual = @(Get-Clipboard | ForEach-Object TrimEnd | Where-Object { $_ })
     if ([string]::Equals(($actual -join "`n"), ($expected -join "`n"), [StringComparison]::Ordinal)) { 'Exact' }
     else { 0..([Math]::Max($expected.Count, $actual.Count) - 1) | Where-Object { -not [string]::Equals($expected[$_], $actual[$_], [StringComparison]::Ordinal) } | ForEach-Object { "Line $($_ + 1): $($actual[$_])"; (([string]$actual[$_]).ToCharArray() | ForEach-Object { 'U+{0:X4}' -f [int]$_ }) -join ' ' } }
     ```

     It prints `Exact` when all 24 lines match, in order. Otherwise, for each line that differs, it prints the line number, the copied line and its character codes. A missing line shows as an empty copied line.
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

**Gate:** pass or fail, for each file. A file passes when Acrobat Reader, Chrome and Firefox all pass it.

Notes: <anything a viewer did differently, with screenshots linked if useful>
```
