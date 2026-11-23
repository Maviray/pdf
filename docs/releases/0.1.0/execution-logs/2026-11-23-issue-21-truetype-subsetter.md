# P1.2 TrueType subsetter: execution log

Issue [#21](https://github.com/Maviray/pdf/issues/21). Plan: [2026-11-23-issue-21-truetype-subsetter.md](../plans/2026-11-23-issue-21-truetype-subsetter.md).

The branch `feature/21-truetype-subsetter` is stacked on `feature/20-truetype-parser` (PR #24), which is stacked on `docs/p1-fonts-spec` (PR #19). Neither was merged yet.

## 2026-11-23

- **Planning.** A throwaway read of the four Noto Sans files supplied the plan's values:
  - every style has `cvt `, `fpgm`, `prep` and `gasp`, and `post` format 2;
  - "Ä" is glyph 134, made of "A" (36) and a mark (106), both simple;
  - no composite nests another, so the nesting tests use built fonts;
  - three styles each have one glyph whose data isn't a multiple of 4 bytes.
- **Task 1, left side bearings:** 2 tests, red (CS1061) then green.
  - The test font builder now writes each glyph's `xMin` as its left side bearing, as real fonts do.
  - Full suite: 369 tests, 357 passed and 12 skipped. The skips are qpdf, poppler and fontTools, which aren't installed on the development machine.
- **Task 2, font file assembler:** 16 tests, red (CS0103) then green. The expected checksums and search fields were worked out by hand from the OpenType formulas.
  - Full suite: 385 tests, 373 passed.
- **Task 3, glyph closure:** 16 tests, red (CS0103) then green. They cover:
  - cycles of one and two glyphs;
  - chains of 16 and 17 composites;
  - the 65,535-glyph limit, with appended components;
  - Noto Sans "Ä" in every style.

  Full suite: 401 tests, 389 passed.
- **Task 4, subsetter:** 23 tests, red then green. The red build failed with CS0246 (the type `FontSubset` was missing) rather than the plan's CS0103, which has the same cause.
  - Full suite: 424 tests, 412 passed.
- **Task 5, damage the subset reaches:** the damage sweep now builds a subset of every glyph of each damaged font, and the format 12 starting font gained a nested composite.
  - The red step was a mutant. Without the component range check in `CompositeGlyph.ReadComponents`, the sweep found escapes in both starting fonts: 32 in format 12 and 12 in format 4. Each was an `ArgumentOutOfRangeException` raised while building the subset.
  - With the check restored, nothing escaped, so there was nothing to fix.
  - Full suite: 424 tests, 412 passed.
- **Task 6, fontTools subset check:** 11 tests, red (CS0117).
  - The script's `subset` command loads the subset with checksum errors raised, decompiles every table, and compares every glyph with its original.
  - The subsets are each Noto Sans style with the gate's text, each style with every glyph in reverse order, and a built font with nested composites.
  - Two self-tests prove the check can fail: wrong glyphs, and a bad checksum.
  - Locally the 11 tests skip with the install message. The PR's CI run is where they first run.
  - Full suite: 435 tests, 412 passed and 23 skipped.
- **First CI run** (PR #25, run 45109860318, at `2a1e855`): 435 of 435 tests succeeded, with 0 skipped. CI's fontTools is 4.61.1. It accepted every subset: the gate text and every glyph reversed, in all four Noto Sans styles, and the nested built font. It rejected both damaged subsets in the self-tests.
- **Whole-branch review** by a fresh reviewer: merge with fixes, all of them in the tests. It found no bug in `src/`.
  - Its own probes found nothing that escapes. It installed fontTools 4.61.1 outside the repository for them, and probed the closure's depth and limit edges and the damage the subset reaches. fontTools also accepted reversed subsets it built itself.
  - **Fixed:** the 65,535-glyph limit had two untested edges: reaching it exactly through appended components, and listing more than 65,535 glyphs. Both tests are in place, red against two mutants (an append check one too strict, and the count check removed), then green.
  - **Fixed:** the bad-checksum self-test changed a byte that also broke the glyph's decoding, so it would pass even without checksum checking. It now changes only the stored checksum of `glyf`. Its red step needs fontTools, so the evidence is the reviewer's run with checking off, which accepted such a file, and CI's run with it on.
  - Full suite: 436 tests, 413 passed and 23 skipped.
  - **Deferred, minor:**
    - damage messages name the table, not the glyph;
    - the first-index rewrite for a glyph listed twice isn't pinned by a test;
    - a truncated composite record is covered only by the sweep;
    - the sweep subsets only the identity list;
    - `Build` checks the embedding permission before its list argument.
