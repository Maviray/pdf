# Viewer check

Status: 2026-11-19

A check by hand that the hello-world file opens cleanly and reads correctly in the viewers CI can't run. It is the P0 gate, and it is repeated for each release (design document, Testing and validation).

This checklist is never ticked. For each run, copy the report template at the end to `docs/qa/reports/YYYY-MM-DD-viewer-check-run.md`, record the verdicts there, and commit it.

## The file

1. Open the latest successful *Build* run of the pull request being checked, on the repository's **Actions** tab. *Build* runs only for pull requests.
2. Download the `generated-pdfs` artifact and unzip it. Artifacts expire after 14 days. If it's gone, re-run the job.
3. Open `Maviray.Pdf.Tests/bin/Release/net10.0/generated/HelloWorldTests/HelloWorldPassesEveryValidator.pdf`. If the path differs, search the unzipped folder for that file name.
4. Note the file's SHA-256 for the report: `Get-FileHash <path> -Algorithm SHA256` in PowerShell. The writer's output is deterministic, so a later commit that produces the same hash is covered by the same verdicts.

The other files in the artifact belong to other tests. Some are broken on purpose, to prove that the validators catch errors, so don't use them for this check.

## What the page should look like

The page is A4 in portrait (210 × 297 mm) and white. It holds exactly these four things, and nothing else:

| Element | What it looks like | Where |
| --- | --- | --- |
| `Hello, world` | Large sans-serif text (Helvetica 24 pt, or the viewer's stand-in for Helvetica) | Left edge 2.5 cm from the page's left edge; baseline about 3.2 cm from the top |
| `Grüße aus Köln – 25 €` | The same font, smaller (14 pt) | Directly below, same left edge; baseline about 4.3 cm from the top |
| Blue rectangle | Solid blue, about 7.1 × 3.5 cm: twice as wide as tall | Left edge in line with the text; top about 6.4 cm from the page's top |
| Red rectangle | The same size, as a thick red outline (about 1.4 mm) with a white inside | To the right of the blue one, with about 1 cm between them, and level with it |

## Viewers

| Viewer | Engine | Make sure that |
| --- | --- | --- |
| Adobe Acrobat Reader | Adobe's own | — |
| Google Chrome | PDFium | the file opens in Chrome's own viewer: drag it into a tab, or press Ctrl+O. Chrome mustn't download it or hand it to an Acrobat extension. |
| Mozilla Firefox | pdf.js | the file opens in Firefox's own viewer. Check that *Settings › Applications › Portable Document Format* is "Open in Firefox". |
| macOS Preview | Apple PDFKit | — |

## Checks, for each viewer

1. **Opens cleanly.** The file opens with no error, warning or "repaired" message.
2. **Looks right.**
   - The page matches the table above: the four elements, their sizes, positions and colours, and nothing else.
   - Check the page size in the viewer's document properties:
     - Acrobat: *File › Properties › Page Size*;
     - Chrome: *⋮ › Document properties*;
     - Firefox: *» › Document Properties*.

     The size is A4, 210 × 297 mm (8.27 × 11.69 in), portrait.
3. **Text copies out exactly.**
   - Select all the text on the page, copy it, then run this in PowerShell. It compares the clipboard with the two lines:

     ```powershell
     $expected = 'Hello, world', "Gr$([char]0xFC)$([char]0xDF)e aus K$([char]0xF6)ln $([char]0x2013) 25 $([char]0x20AC)"
     $actual = @(Get-Clipboard | ForEach-Object TrimEnd | Where-Object { $_ })
     if (($actual -join "`n") -ceq ($expected -join "`n")) { 'Exact' } else { $actual | ForEach-Object { ($_.ToCharArray() | ForEach-Object { 'U+{0:X4}' -f [int]$_ }) -join ' ' } }
     ```

   - It prints `Exact` when the text matches. Otherwise it prints each copied line as character codes, so a hyphen (U+002D) instead of the en dash (U+2013), or a no-break space, shows up.
   - Line breaks, blank lines and spaces at the end of a line don't count, because viewers paste them differently. Every character within each line must match.
4. **Acrobat only: no repair.** Close the file. Acrobat must not ask whether to save changes, because asking means it repaired the file.

A viewer passes when all its checks pass. A viewer that can't be checked, such as Preview without a Mac, is recorded as "not checked", with the reason.

## Report template

```markdown
# Viewer check run, YYYY-MM-DD

Checklist: [viewer-check.md](../checklists/viewer-check.md). File: `HelloWorldTests/HelloWorldPassesEveryValidator.pdf`,
SHA-256 `<hash>`, from the `generated-pdfs` artifact of CI run <run link>, commit <sha>. Checked by <name> on
<OS and version>.

| Viewer | Version | Opens cleanly | Looks right | Text copies exactly | No repair (Acrobat) | Verdict |
| --- | --- | --- | --- | --- | --- | --- |
| Adobe Acrobat Reader | | | | | | |
| Google Chrome | | | | | n/a | |
| Mozilla Firefox | | | | | n/a | |
| macOS Preview | | | | | n/a | |

**Gate:** pass or fail. It passes when Acrobat Reader, Chrome and Firefox all pass.

Notes: <anything a viewer did differently, with screenshots linked if useful>
```
