# Viewer check

Status: 2026-11-19

A check by hand that the hello-world file opens cleanly and reads correctly in the viewers CI can't run. It is the P0 gate, and it is repeated for each release (design document, Testing and validation).

This checklist is never ticked. For each run, copy the report template at the end to `docs/qa/reports/YYYY-MM-DD-viewer-check-run.md` and record the verdicts there.

## The file

1. Open the latest successful *Build* run of the pull request or branch being checked, on the repository's **Actions** tab.
2. Download the `generated-pdfs` artifact and unzip it.
3. Open `generated/HelloWorldTests/HelloWorldPassesEveryValidator.pdf`.

The other files in the artifact belong to other tests. Some are broken on purpose, to prove that the validators catch errors, so don't use them for this check.

## What the page should look like

An A4 page in portrait, white, with:

- **Near the top left:** `Hello, world` in a large sans-serif font (Helvetica, or the viewer's stand-in for it).
- **Just below it:** `Grüße aus Köln – 25 €` in a smaller size.
- **About two-thirds of the way up:** two rectangles side by side.
  - On the left, a solid blue rectangle.
  - On the right, a red outline of the same size with a white inside.

## Viewers

| Viewer | Engine |
| --- | --- |
| Adobe Acrobat Reader | Adobe's own |
| Google Chrome | PDFium |
| Mozilla Firefox | pdf.js |
| macOS Preview | Apple PDFKit |

## Checks, for each viewer

1. **Opens cleanly.** The file opens with no error, warning or "repaired" message.
2. **Looks right.** The page matches the description above: the text, the two rectangles and their colours, and A4 portrait.
3. **Text copies out exactly.**
   - Select all the text on the page, copy it, and paste it into a plain-text editor.
   - The two lines must be exactly `Hello, world` and `Grüße aus Köln – 25 €`.
   - Check the umlauts, the `ß`, the dash (an en dash) and the euro sign.
4. **Acrobat only: no repair.** Close the file. Acrobat must not ask whether to save changes, because asking means it repaired the file.

A viewer passes when all its checks pass. A viewer that can't be checked, such as Preview without a Mac, is recorded as "not checked", with the reason.

## Report template

```markdown
# Viewer check run, YYYY-MM-DD

Checklist: [viewer-check.md](../checklists/viewer-check.md). File: `HelloWorldTests/HelloWorldPassesEveryValidator.pdf` from the
`generated-pdfs` artifact of CI run <run link>, commit <sha>. Checked by <name> on <OS and version>.

| Viewer | Version | Opens cleanly | Looks right | Text copies exactly | No repair (Acrobat) | Verdict |
| --- | --- | --- | --- | --- | --- | --- |
| Adobe Acrobat Reader | | | | | | |
| Google Chrome | | | | | n/a | |
| Mozilla Firefox | | | | | n/a | |
| macOS Preview | | | | | n/a | |

Notes: <anything a viewer did differently, with screenshots linked if useful>
```
