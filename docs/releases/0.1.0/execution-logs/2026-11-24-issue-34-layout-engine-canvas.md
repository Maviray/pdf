# P1.9 Layout engine and canvas: execution log

Issue [#34](https://github.com/Maviray/pdf/issues/34). Plan: [2026-11-24-issue-34-layout-engine-canvas.md](../plans/2026-11-24-issue-34-layout-engine-canvas.md).

The branch `feature/34-layout-engine-canvas` is stacked on `feature/33-text-gate` (PR #43), which is stacked on #42 and #41.

## 2026-11-24

- **Planning.** The plan settles five things the spec leaves open:
  - a measurement carries the element it comes from;
  - `Draw` returns whether it advanced;
  - the content's `None` always throws;
  - the pass-limit error names `Document` with no available size;
  - the canvas keeps its own offsets.
  - **Self-check:** 30 blocks of 100 points at 7 per page fill 5 pages, not 7.
- **Task 1, the protocol and `LayoutException`:**
  - **Ruling:** `Element.Draw` takes an `ICanvas`, which the plan creates in Task 2, so the interface came first.
  - **Ruling:** the path test needs a parent and a child, and one type per file rules out nested test types, so `FixedBlock`, `TestStack` and `PageCountBlock` were created here.
  - The analyzers accepted `LayoutException` with its standard constructors, which give an empty path and NaN sizes.
- **Task 2, the canvases:**
  - **Slip:** the canvases were written before their tests were run red; the tests could only have failed to compile.
  - Mutants made up for it. A canvas that doesn't flip y fails three tests. Text that ignored the offset survived every test, so `ATextLineFollowsTheOffset` was added, and it fails under that mutant.
  - **Ruling:** `ContentStreamBuilder` already writes a fill colour only when it changes, so the canvas doesn't track it. The decoration test checks the colour in force, not an `rg` of the rectangle's own.
- **Task 3, the paging loop and the passes:**
  - **Ruling:** the one-pass test needs an element that records its draws without showing the page count, so `PageCountBlock` gained a flag instead of a new test type.
  - **Mutants**, each failing the test meant for it:
    - no reset after a page;
    - no reset before a pass, which four tests catch;
    - the render flag off;
    - the footer measured at the full inner height.
  - A mutant that drops the no-progress check would page forever, so it wasn't run. `ContentThatSplitsWithoutAdvancingThrows` shows the throw.
- **Task 4, PDF pages:** two page sets, portrait and landscape, render every block where the recording canvas saw it, and qpdf accepts the file. A PDF canvas that doesn't flip y fails the check, on page 2: page 1's mirrored blocks happen to cover their own centres.
- **Full suite:** Release, 855 tests, all passed, with every validator required.
- **Whole-branch review** by a fresh reviewer: merge with fixes, none of them critical.
  - It ran the suite with every validator, and probes and mutants in a worktree it removed afterwards. It installed nothing.
  - It confirmed the passes, the reset before every pass, the y flip everywhere, the paths, and that `LayoutException` is the only public type.
  - **Fixed, each with a test that failed first:**
    - **A draw that finishes an element didn't count as progress.** A stack moving past a finished child whose draw returned false threw "split across pages but drew nothing" on a valid document. P1.10's Column would have copied it.
      - `Element.Draw`'s contract now says a draw after `Full` advances, and that a container advanced if a child advanced or its cursor moved.
      - `ElementContract` checks it, and `MovingPastAFinishedChildCountsAsProgress` pages the reviewer's case onto two pages.
    - **Errors reported the slot's space, not the named element's.** A block inside an inset said "The space is 495 × 722 pt", when it had 295.
      - A measurement now carries the space its source was measured in, and a container passes a child's split up with `WithSize`.
      - Both errors report it: 295 for the block, 395 for a stall inside an inset of 100. An infinite width, which Row gives an Auto item, is written as ∞.
    - **The pass limit wasn't pinned.** A limit of 4 passed every test. The oscillation test now asserts exactly three passes.
    - **The header was written after the content.** Tools that read a page's stream in order, such as screen readers on untagged files, met the body first. The header is now drawn first, and the footer last.
    - **A broken no-progress check would have hung its test,** paging forever. The recording canvas stops at 1,000 pages. The mutant that drops the check, which couldn't be run safely before, now fails two tests.
    - The contract check's test for a stalling element asserts its own message.
  - **Full suite:** Release, 862 tests, all passed; Debug, 862 tests, 861 passed and 1 skipped (the allocation test, Release only).
  - **Deferred, minor:**
    - drawing the header and footer at their measured height isn't pinned. P1.10's Align and Background draw at measured sizes, and can add the probe.
    - `PdfCanvas`'s font cache duplicates `PdfDocumentWriter.EmbedFont`'s own, so `EachFontIsEmbeddedOnce` can't fail, and pdffonts isn't asked.
    - the error builder is private to `LayoutEngine`. P1.10's Row and P1.11's Text throw from inside `Measure`, and will move it to `LayoutErrors`.
    - the pass-limit message names the first three counts instead of joining them.
  - **Rulings on what the reviewer set aside:**
    - an error on page N comes after N − 1 pages reached the output. `Save`'s behaviour on failure is L-11's, in P1.12.
    - no page sets, an open page after an exception, and a diagnostics sink in `PageContext` belong to P1.12 and P1.11.
    - two `TrueTypeFont` instances of one file embed twice. Font identity is the registry's (L-09).
    - containers that measure their children again in `Draw` cost more with depth. That is P1.10's and P1.11's design.
    - a header whose height changes per page makes "the content's `None` always throws" slightly strict. The engine follows L-04 as written.
