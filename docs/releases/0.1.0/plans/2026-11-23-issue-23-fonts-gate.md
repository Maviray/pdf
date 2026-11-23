# P1.4 Fonts gate: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The P1 fonts gate of F-15. One A4 page carries six languages in the four Noto Sans styles, and a test checks it with every validator. The viewer checklist describes the file, so the maintainer can check it by hand.

**Architecture:** One test writes the page with P1.3's embedded fonts and checks it:
- qpdf accepts it;
- pdffonts reports four embedded, subset, Unicode-mapped fonts;
- pdftotext reads back all 24 lines;
- the render has ink inside each line's box and nothing outside;
- writing it twice gives the same bytes.

The test also saves the expected text next to the PDF, for the clipboard check in the viewer checklist.

**Tech Stack:** .NET 10, C#, xUnit v3; qpdf and poppler-utils in CI.

**Spec:** [P1 Fonts](../specs/2026-11-20-p1-fonts-design.md), decision F-15, with F-13 and F-16. Work item [#23](https://github.com/Maviray/pdf/issues/23). The branch `feature/23-fonts-gate` is stacked on `feature/22-embedded-truetype-fonts` (PR #26).

## Global Constraints

- The P1.3 constraints hold. This item changes no code in `src/`.
- The page:
  - A4 (595.2756 × 841.8898 points), compressed with `CompressionLevel.Optimal`;
  - written with `PdfDocumentWriterTests.Write`, so the file ID is fixed;
  - saved with `GeneratedFiles.Save`, so CI uploads it.
- Expected text is the six lines of F-15, copied exactly. Expected boxes come from the font's advance widths and `hhea` metrics, which P1.1 checked against fontTools.
- The checklist is a living doc: it describes what runs today and gets a new `Status:` date (managing-project-docs).
- Each task ends with one commit, subject `#23 <summary>`. The records are the last commit.

## Review Focus

1. **A missing glyph.** No character of the gate is drawn as `.notdef`. A planning probe found all 74 distinct characters in all four styles, and the test asserts it again. Pinned in Task 1.
2. **Lines in the wrong order or merged.** pdftotext must return 24 separate lines, in page order. The leading of 24 points keeps lines apart. Pinned in Task 1.
3. **Ink outside the text.** The page has a stray mark, or a line wider than its box, for example a bolder font drawn in a regular line's place. Pinned in Task 1.
4. **Determinism.** Two writes give equal bytes, so the viewer check's SHA-256 identifies the file. Pinned in Task 1.
5. **A checklist that doesn't match the file.** It names the wrong path, size or layout, or a clipboard check that can't succeed. Checked in Task 2 against the test's constants.

---

### Task 1: The gate test

**Files:**
- Test: `tests/Maviray.Pdf.Tests/Writer/FontsGateTests.cs`

**The page.** Line *i* (0 to 23) uses style *s* = *i* ÷ 6 and language *l* = *i* mod 6:
- styles, in order: `Regular`, `Bold`, `Italic`, `BoldItalic`;
- languages, in F-15's order: German, Turkish, Azerbaijani, Russian, Ukrainian, Greek.

Each line is Noto Sans at 14 pt, set with `SetTextMatrix(1, 0, 0, 1, 72, y)` where `y = 790 − 24 × i − 12 × s`. The extra 12 points separate the style blocks; the last baseline is 202. Characters are encoded with `EmbeddedText.Encode`.

- [ ] **Step 1: Test.** `FontsGateTests.FontsGatePassesEveryValidator` writes the page and saves it. It also saves the 24 lines, joined with LF plus a final LF, as UTF-8, with `GeneratedFiles.SaveAs(…, ".txt")`. Then:
  The checks run in this order, so the ones the development machine's tools allow (only xpdf's pdftotext is installed there) run before the first missing tool skips the rest:
  1. Every character of the six lines has a glyph other than 0 in each style (`TestFonts.NotoSans(style).GetGlyphId`).
  2. `Poppler.ExtractText(path)`, split on LF, each line trimmed at the end, empty lines dropped, equals the 24 lines in order. This is the same blind spot as the P0 gate.
  3. Writing the page again gives equal bytes.
  4. `Poppler.Render(path)` gives one page. Line *i*'s box is:
     - horizontally, from 69 to 75 + *w*, where *w* is the sum of its glyphs' advances × 14 ÷ 1000;
     - vertically, from `y − 6.1 − 2` to `y + 15 + 2` in PDF space, from `hhea` 1069 and −293 at 14 pt;
     - flipped to pixel rows with `Height − y`, as the P0 gate does.

     Each box holds at least one pixel with a channel below 128, and every pixel outside all 24 boxes is white. The boxes together cover less than half the page, so the check can't pass by covering everything, and the ink inside each box proves the render isn't blank.
  5. `Qpdf.Check(path)`.
  6. `Poppler.ListFonts(path)` reports four fonts:
     - each `CID TrueType`, embedded, subset and Unicode-mapped;
     - each named `^[A-Z]{6}\+`, followed by the four names `NotoSans-Regular`, `NotoSans-Bold`, `NotoSans-Italic` and `NotoSans-BoldItalic`, as a set.
- [ ] **Step 2:** Run it. Locally, checks 1 to 3 run, and the test skips at check 4. In CI it runs in full.

  A gate test can't fail before the code exists, because P1.3 already built it. Its red steps are mutants of the test that the local checks catch. Watch each fail, then restore it:
  - a CJK character added to one line fails check 1;
  - the Russian and Ukrainian lines swapped in the expected text fail check 2.

  Checks 4 to 6 first run in CI. Their own assertions keep them from passing vacuously: ink in every box, under half the page in boxes, and exactly four fonts.
- [ ] **Step 3:** Verify the whole suite passes. Commit `#23 add the fonts gate`.

### Task 2: The viewer checklist

**Files:**
- Modify: `docs/qa/checklists/viewer-check.md`

- [ ] **Step 1:** Add the fonts file, keeping the hello-world file as it is.
  - **The files:** a second file, `Maviray.Pdf.Tests/bin/Release/net10.0/generated/FontsGateTests/FontsGatePassesEveryValidator.pdf`, with its expected text `FontsGatePassesEveryValidator.txt` beside it. The report records the SHA-256 of both PDFs.
  - **What the fonts page should look like:** A4, white, 24 lines of Noto Sans at 14 pt with a left edge 2.5 cm from the page's left edge.
    - The first baseline is about 1.8 cm from the top, and the last about 22.6 cm from the top.
    - The lines form four blocks of six, in regular, bold, italic and bold italic, separated by small gaps.
    - Each block has the six lines of F-15.
    - No character shows as an empty box, and every accent sits on its letter.
  - **Text copies out exactly:** the same rules as the hello-world file. The comparison reads the expected lines from the `.txt` file, so non-ASCII characters needn't be typed:

    ```powershell
    $expected = @(Get-Content -Encoding UTF8 '<path>\FontsGatePassesEveryValidator.txt')
    $actual = @(Get-Clipboard | ForEach-Object TrimEnd | Where-Object { $_ })
    if (($actual -join "`n") -ceq ($expected -join "`n")) { 'Exact' } else { Compare-Object $expected $actual }
    ```

  - **Report template:** a second table, for the fonts file.
  - **The gate line:** the hello-world file is the P0 gate. The fonts file joins the 0.1.0 release check, and it isn't a merge condition (F-15).
  - **The status date:** 2026-11-23.
- [ ] **Step 2:** Check the checklist against Task 1's constants: the path, 14 pt, 72 points (2.54 cm), the first baseline (841.8898 − 790 = 51.9 points, 1.83 cm), the last (639.9 points, 22.57 cm), and the line order.
- [ ] **Step 3:** Commit `#23 describe the fonts gate file in the viewer checklist`.

### Task 3: Records

- [ ] Write `docs/releases/0.1.0/execution-logs/2026-11-23-issue-23-fonts-gate.md`.
- [ ] Set #23's row in the release index to "In review", with the plan and the log.
- [ ] Commit `#23 add the P1.4 plan and execution log`.
- [ ] Push, and open the PR into `develop` with `Refs #23`. Its description says it is stacked on #26, #25, #24 and #19.
- [ ] Record the first CI run in the log: tests run, and 0 skipped.
