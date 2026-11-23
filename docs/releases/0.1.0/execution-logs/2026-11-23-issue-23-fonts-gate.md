# P1.4 Fonts gate: execution log

Issue [#23](https://github.com/Maviray/pdf/issues/23). Plan: [2026-11-23-issue-23-fonts-gate.md](../plans/2026-11-23-issue-23-fonts-gate.md).

The branch `feature/23-fonts-gate` is stacked on `feature/22-embedded-truetype-fonts` (PR #26), which is stacked on #25, #24 and #19. None of them was merged yet.

## 2026-11-23

- **Planning.** A throwaway read of the four Noto Sans files found every one of the gate's 74 distinct characters in every style. The development machine has only xpdf's pdftotext, so the plan orders the checks to run locally as far as that tool allows.
- **Task 1, the gate test:** `FontsGateTests.FontsGatePassesEveryValidator`.
  - **Plan slip:** the line box's vertical extent is computed from `hhea` as the plan says (14.97 and 4.10 points at 14 pt). The plan's "6.1" was an arithmetic slip.
  - **xpdf's default reading order doesn't separate this page's lines.** xpdf 4.00's default mode joins each block's six lines into one output line, giving four lines with all the text present. Its `-raw` and `-layout` modes return all 24 lines in order, so the file is right. The text check therefore moved after the render check, and runs where poppler, the reference reader, is installed.
    - The first version of this entry said xpdf repeated two lines and dropped the rest. That came from reading its output cut at 160 characters. The review corrected it, and showed that xpdf does the same to plain Helvetica lines, so nothing in this file triggers it.
  - **Red step:** a mutant. With "中" added to the Azerbaijani line, the glyph check failed with "Noto Sans Regular has no glyph for 中."
  - Locally the glyph and same-bytes checks pass, and the test skips at the render, because pdftoppm is missing.
  - Full suite: 502 tests, 475 passed and 27 skipped.
- **Task 2, the viewer checklist:**
  - It describes the fonts file next to the hello-world file: its path, its layout, what to look out for, and a clipboard check that reads the expected lines from the `.txt` file the test writes beside the PDF.
  - The report template gains a table for the fonts file.
  - The layout numbers were checked against the test's constants: 2.54 cm, 1.83 cm and 22.57 cm.
- **First CI run** (PR #27, run 45115240941, at `e92f77a`): 502 of 502 tests succeeded, with 0 skipped. The gate passed every check, including the render boxes, poppler's pdftotext on all 24 lines, qpdf and pdffonts.
- **Whole-branch review** by a fresh reviewer, which was told not to download or install anything: merge with fixes, none of them critical.
  - It measured the fonts' glyph boxes and found at least 2.3 points of margin for the render check.
  - It confirmed the same bytes across two processes, and the same code points in the spec, the test, the checklist and the `.txt`.
  - **Fixed:** the checklist's clipboard checks compared by PowerShell's culture rules. `-ceq` counts a decomposed `ö` as equal, and `Compare-Object` prints nothing for swapped lines. Both snippets now compare ordinally, and the fonts snippet names each differing line with its character codes.
    - The old and new comparisons were run in PowerShell 7.7.0 and 5.1. The old one passed a decomposed `ö` and stayed silent on swapped lines; the new one caught both.
    - The checklist's own snippet was run against the generated `.txt`, exact and with two lines swapped.
  - **Corrected:** the xpdf description above, and the checklist's accent list (`İ` has one dot, and `Ə` and `ə` are letters of their own).
  - Full suite: 502 tests, 475 passed and 27 skipped.
  - **Deferred, minor:**
    - a Windows contributor's Git Bash xpdf can shadow poppler's pdftotext;
    - "ink in every box" is met by one dark pixel;
    - the pixel-centre formula is 0.11 points off.
- **CI after the fixes** (run 45117038740, at `8f50044`): 502 of 502 tests succeeded, with 0 skipped.
