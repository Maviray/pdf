# P1.10 Containers: execution log

Issue [#35](https://github.com/Maviray/pdf/issues/35). Plan: [2026-11-24-issue-35-containers.md](../plans/2026-11-24-issue-35-containers.md).

The branch `feature/35-containers` is stacked on `feature/34-layout-engine-canvas` (PR #44), and so on #43, #42 and #41.

## 2026-11-24

- **Planning.** The plan's six rulings:
  - every child is drawn at its measured size;
  - Column spacing goes only between items that take height;
  - Border's box is the width it is drawn at;
  - the size limits are one element, named by their call;
  - an alignment at infinite width sits at x = 0;
  - the error builder moves into `LayoutErrors`, which P1.9's review had deferred.
  - **Self-check:** the Row example's flex shares were wrong. In 495 points with spacing 5, weights of 1 and 3 share 320 points as 80 and 240.
- **Task 1, shared pieces:**
  - `ElementContract` measures each element again at its measured size, and at an infinite width.
  - `PickyBlock`, which is `Full` only with a point to spare, fails the new check, which the test asserts.
  - After the branch switch, git had written the committed files with CRLF. A small edit helper that keeps each file's line endings replaced the inline patches.
- **Task 2, Column:** a mutant that adds spacing after an item with no height fails `AnItemWithNoHeightTakesNoSpacing`.
- **Task 3, Row:**
  - **Ruling:** a finished item measures 0 wide, so a row split across pages would move its later items left. A Row keeps the widths of its first draw while its width stays the same. *Replaced after the review:* a Row remembers only each auto item's width at its finish. `AFinishedAutoItemKeepsItsWidthOnLaterPages` was written after that code, so it passed at once; a mutant without the widths kept fails it.
  - A mutant whose flex shares ignore the auto widths fails three tests.
  - Row's contract sizes stop at 70 × 5: below its fixed and auto widths a Row throws by design.
- **Task 4, padding, alignment and size limits:**
  - **Ruling:** L-06's "a space of zero or less means None" for Padding, read literally, breaks the measured-size rule for a child with no size. A space below zero is `None`, and at zero the child is measured. Border follows the same rule.
  - A mutant where Column draws items at the height left, instead of at their own height, fails `InAColumnItAlignsWithinTheItem`.
- **Task 5, background, border, lines and the page break:**
  - **Ruling:** the background, border and line tests share one class, `DecorationTests`.
  - Two page breaks in a row give one empty page, and the content after a break starts at the top of the next page.
- **Task 6, a composed page:**
  - **Ruling:** the plan's red step, a Row mutant, can't fail this test, because it compares the PDF render with the recording canvas, and a layout mutant moves both. The red step is the PDF canvas's y flip, and the test also pins the right-aligned total at x = 420.
  - qpdf accepts the three pages, and every rectangle renders in the colour drawn there last.
- **Full suite:** Release, 943 tests, all passed, with every validator required.
- **Whole-branch review** by a fresh reviewer: merge with fixes. One finding was critical, and four were important.
  - It reproduced each finding with a probe in a worktree it removed afterwards. It installed nothing.
  - **Fixed, each with a test that failed first:**
    - **Content was silently dropped.** A Column drawn at its measured height adds its lengths in another order than it measured them. With `HorizontalLine(14.4)`, spacing 1.5 and `HorizontalLine(0.5)`, the last line got 0.4999… points and was never drawn, with no error. The reviewer found it in 102 of 432 realistic combinations.
      - Every fit test now goes through one `LayoutTolerance`, a millionth of a point.
      - Column measures and draws in one loop.
      - Row decides that an item finished at the height it draws it at.
      - `NoFractionalColumnLosesAnItem` sweeps 500 random fractional columns with its seed logged. An exact fit for lines fails it and three more tests.
    - **An auto Row of flex items threw.** At an infinite width it reported the sum of its items' widths, where its equal shares didn't hold the wider item. It now reports the width at which every share holds its item, and the contract measures again at the width found at infinity.
    - **Frozen widths threw when an unfinished auto item widened on a later page,** and didn't hold under an `Align`, which draws the Row at its measured width. A Row now remembers only each auto item's width at its finish.
    - **An auto item that fit nothing was measured at width 0,** where a Text element would throw instead of moving to the next page. The Row now returns its `None`, and measures each auto item once per call.
    - **An empty Column or Row** broke the progress rule: its draw after `Full` said it hadn't advanced.
  - Two plan tests were missing, a wider child of `Width(w)` and `MinHeight` through the engine. They now pass, pinning what already worked.
  - **Full suite:** Release, 956 tests, all passed; Debug, 956 tests, 955 passed and 1 skipped (the allocation test, Release only).
  - **Deferred, minor:**
    - a zero-height item before one that can never fit gives a blank page, and the error a page late;
    - nested Auto Rows measure exponentially often by depth. P1.11's Text element must keep an infinite-width fill besides its last finite one;
    - SizeLimit, Padding and Border give `None` without the size they need, and the length checks are borrowed rather than shared.
  - **For P1.12:** chained alignments don't combine, because an outer Align draws the inner one at its measured size. P1.12's builders merge `.AlignRight().AlignBottom()` into one Align.
  - **For the user to decide:** a Background or Border in a Row cell covers the child's height, not the row's, as L-06 says. Invoice rows with cell backgrounds come out ragged. Changing it means drawing at the height given, and the engine drawing the content at its measured height.
