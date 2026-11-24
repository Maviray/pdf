# P1.14 Invoice gate: execution log

Issue [#39](https://github.com/Maviray/pdf/issues/39). Plan: [2026-11-24-issue-39-invoice-gate.md](../plans/2026-11-24-issue-39-invoice-gate.md).

The branch `feature/39-invoice-gate` is stacked on `feature/38-default-fonts-package` (PR #48), and so on #47 to #41.

## 2026-11-24

- **Planning.** The plan's rulings:
  - the column headings are in the header, as L-17 says, so on page 1 they stand above the addresses;
  - each description is unique, ends with a three-digit lot number, and fits on one line;
  - the page-1 clipboard check looks for strings, not lines, because viewers paste a row's cells differently;
  - "no ink outside the margins" looks at 72 dpi for any pixel with a channel under 128 outside the margin box.
- **Task 1, the invoice and its data:**
  - the parties, the items, the totals with VAT per rate, rounded half away from zero;
  - amounts the German way, such as `1.234,56 €`, from a `NumberFormatInfo` of its own rather than culture data;
  - ten item templates with Turkish and Cyrillic names;
  - the document, built only through the public API with `RegisterNotoSans()` on a registry of its own.
  - **Ruling:** the invoice record is `InvoiceData`, not `Invoice`, because a type named like its namespace makes references ambiguous. The plan is amended to match.
- **Task 2, the gate:**
  - red at 420 items, which made 11 pages;
  - a full page holds 40 items, so 385 items make exactly 10:
    - page 1 holds items 1 to 35, below the addresses;
    - pages 2 to 9 hold 40 each;
    - page 10 holds items 356 to 385 and the totals.
  - **Ruling:** the plan's red step said one item more or fewer fails. But a range of counts makes 10 pages, so the red was the 11-page count, and mutants proved the other checks:
    - an item drawn twice;
    - margins 0.5 cm narrower than the check.
  - pdftotext reads the narrow `7 %` cell as `7%` in its layout and default modes, though the words have the same gap as in `19 %`, and `-raw` reads the space. No check uses that cell.
- **Task 3, the viewer checklist:**
  - the invoice's file, its description and its report table;
  - a snippet that finds each of page 1's 18 strings in the copied text.
  - The description was checked against the real file. The snippet was run on page 1's extracted text: it prints `Exact`, and finds a dotless `ı` swapped for `i`.
  - A first description of the page ranges was read from a file a margin mutant had written. It was caught before the commit, and the checklist has the real ranges.
- **Full suite:**
  - Release: 1,150 of 1,150;
  - Debug: 1,149 passed and 1 skipped.
- **Whole-branch review** by a fresh reviewer: ready with fixes, with no critical finding.
  - It ran the invoice tests with poppler 25.12 as well as 26.09, and their layout text is byte-identical.
  - It recomputed the totals, and ran the checklist's snippet in PowerShell 7 and 5.1.
  - **Fixed. Each check failed first under a mutant that had passed the old gate:**
    - **"Exactly once" missed an earlier copy.** Item 300 drawn again after item 2 passed, because the check searched only from the previous match. It now compares the match with the first and last occurrence in the whole text.
    - **The gate didn't pin what the checklist states.** Page 1's address block 30 pt taller kept 10 pages and passed. The gate now pins each page's first and last item and the four totals as literals, and its doc comment says to keep the test, the layout and the checklist in step.
    - **The date wasn't checked on every page.** The date moved from the header into page 1's content passed. The reviewer graded this minor; it is fixed because L-17's header holds the date, repeated on every page.
  - **Full suite:**
    - Release: 1,150 of 1,150;
    - Debug: 1,149 passed and 1 skipped.
  - **Deferred minors:**
    - the fonts check passes over an empty list, though the text checks would catch missing fonts;
    - the headings regex lets a line break stand between headings;
    - the data test doesn't pin each of `İ ı ş ğ ё й`;
    - the invoice snippet prints the expected string's codes on a miss, not the pasted text's;
    - "built only through the public API" is held by review, not by a test;
    - the buyer data has no Cyrillic name. Read collectively, L-17 is met by the descriptions.
  - **Rulings on what the reviewer set aside:**
    - identical bytes across platforms is the writer's property;
    - what real viewers paste is the check by hand's job;
    - pdftotext's `7%` is its heuristic;
    - the headings above the addresses follow plan ruling 1;
    - the ink check can't see a shift smaller than the glyphs' own inset, and the page ranges now pin the layout;
    - `Create(itemCount)` names the knob that sizes the document.
- **CI** on `87b4f03`, [run 45378399592](https://github.com/Maviray/pdf/actions/runs/45378399592): 1,150 of 1,150 tests passed, and without ICU the text tests passed 306 of 306. The gate passes in CI, as the work item requires. The PR is [#49](https://github.com/Maviray/pdf/pull/49).
