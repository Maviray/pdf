# P1.4 Fonts gate: execution log

Issue [#23](https://github.com/Maviray/pdf/issues/23). Plan: [2026-11-23-issue-23-fonts-gate.md](../plans/2026-11-23-issue-23-fonts-gate.md).

The branch `feature/23-fonts-gate` is stacked on `feature/22-embedded-truetype-fonts` (PR #26), which is stacked on #25, #24 and #19. None of them was merged yet.

## 2026-11-23

- **Planning.** A throwaway read of the four Noto Sans files found every one of the gate's 74 distinct characters in every style. The development machine has only xpdf's pdftotext, so the plan orders the checks to run locally as far as that tool allows.
- **Task 1, the gate test:** `FontsGateTests.FontsGatePassesEveryValidator`.
  - **Plan slip:** the line box's vertical extent is computed from `hhea` as the plan says (14.97 and 4.10 points at 14 pt). The plan's "6.1" was an arithmetic slip.
  - **xpdf's default reading order garbles this page.** xpdf 4.00's default mode repeated the first two lines four times and dropped the rest. Its `-raw` and `-layout` modes returned all 24 lines in order, so the file is right. The text check therefore moved after the render check, and runs where poppler, the reference reader, is installed.
  - **Red step:** a mutant. With "中" added to the Azerbaijani line, the glyph check failed with "Noto Sans Regular has no glyph for 中."
  - Locally the glyph and same-bytes checks pass, and the test skips at the render, because pdftoppm is missing.
  - Full suite: 502 tests, 475 passed and 27 skipped.
- **Task 2, the viewer checklist:**
  - It describes the fonts file next to the hello-world file: its path, its layout, what to look out for, and a clipboard check that reads the expected lines from the `.txt` file the test writes beside the PDF.
  - The report template gains a table for the fonts file.
  - The layout numbers were checked against the test's constants: 2.54 cm, 1.83 cm and 22.57 cm.
