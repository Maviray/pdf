# P0.3 Content-stream builder, with rectangles: execution log

Issue [#11](https://github.com/Maviray/pdf/issues/11). Plan: [2026-11-19-issue-11-content-stream-builder.md](../plans/2026-11-19-issue-11-content-stream-builder.md).

The branch `feature/11-content-stream-builder` is stacked on `feature/10-streaming-file-writer` (PR #15), which is stacked on PR #14. Both were still waiting to be merged.

## 2026-11-19

- **Task 1, `RgbColor`:** 5 tests, red then green. Full suite: 147 passed.
- **Task 2, `ContentStreamBuilder`:** 25 tests, red then green.
  - One test beyond the plan, `UsingTheBuilderAfterFinishThrows`, pins the plan's statement that the builder can't be used after `Finish`.
  - The tracked values are a private nested `record struct`, which the one-type-per-file rule allows.
  - Full suite: 172 passed.
- **Task 3, page content:** 2 tests, red then green.
  - The plan expected `/Length 23` for `72 600 200 100 re\nf\n`. The bytes are 20, and the test uses 20.
  - `EndPage` finishes the content before writing anything, so an open path throws with nothing written.
  - Full suite: 174 passed.
- **Task 4, rectangles in the render:** 1 test. It skips locally, because qpdf and pdftoppm aren't installed, so its first run is in the PR's CI.
  - Its layout is asymmetric, so the y flip is pinned: the unflipped point for the blue rectangle, (172, 192), is white.
  - A malformed operator also fails it, because `Poppler.Render` fails on anything poppler writes to stderr.
- **First CI run** (PR #16, run 44525834587, at `5c14116`): 175 of 175 tests succeeded, with 0 skipped.

## Final review

A fresh reviewer found no critical issues and three important ones, fixed in one pass:

- **A throwing call left half an operator behind.** Operands were checked while they were written. So `Rectangle(1, 2, NaN, 4)` threw after writing `1 2 `, and a later `rg` became an operator with five operands. A refused line width also became the tracked value.
  - Every operand is now checked before anything is written or tracked.
  - `AThrowingCallWritesNothing` and `AnInvalidLineWidthIsRefusedEveryTime` failed before the fix.
- **Negative line widths were written.** ISO 32000-1 requires the width to be non-negative.
  - `SetLineWidth` now refuses them, and `RefusesANegativeLineWidth` failed before the fix.
  - Zero stays legal: it draws the thinnest line.
- **"EndPage throws with nothing written" wasn't pinned.** `APageCanBeEndedAgainAfterItsContentIsFixed` leaves the path open, then the `q`, then fixes both and ends the page.
  - It checks the object numbers in the file.
  - It fails against a mutant that reserves an object before finishing the content.

The minor findings are left for later:
- the content buffer stays reachable while a caller holds the page;
- three small untested paths;
- deciding redundancy on the written bytes rather than on the raw doubles.
