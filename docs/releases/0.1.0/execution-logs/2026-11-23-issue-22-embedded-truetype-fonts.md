# P1.3 Embedded TrueType fonts: execution log

Issue [#22](https://github.com/Maviray/pdf/issues/22). Plan: [2026-11-23-issue-22-embedded-truetype-fonts.md](../plans/2026-11-23-issue-22-embedded-truetype-fonts.md).

The branch `feature/22-embedded-truetype-fonts` is stacked on `feature/21-truetype-subsetter` (PR #25), which is stacked on #24 and #19. None of them was merged yet.

## 2026-11-23

- **Planning.** Three values the spec leaves open were settled in the plan:
  - the subset tag hashes the base name and the CIDs' glyphs, not their characters, because the font program depends only on the glyphs;
  - `/StemV` clamps the weight to 100–900, because below 55 F-09's formula goes negative;
  - the PostScript name is cut to 63 characters before the tag is added.

  The expected tags were computed with Python's `hashlib`.
- **Task 1, ToUnicode CMap:** 3 tests, red (CS0103) then green. The first green run failed in the test, not the code: its filter for entries also matched the code space line `<0000> <FFFF>`.
  - Full suite: 439 tests, 416 passed and 23 skipped. The skips are qpdf, poppler and fontTools, which aren't installed on the development machine.
- **Task 2, name, widths and descriptor:** 29 tests, red (CS0103) then green. They include the name rules, scaling from 2048 units per em, every flag and StemV case, and the four Noto Sans styles.
  - Full suite: 468 tests, 445 passed.
- **Task 3, embedded font:** 13 tests, red (CS0246) then green.
  - `EmbeddedFont` builds every value before it writes the first object, so a refused subset writes nothing.
  - P0's `FontResources` and `StandardFont` tests passed unchanged.
  - Full suite: 481 tests, 458 passed.
- **Task 4, content streams and the document:** 11 tests, red (CS1061, CS1503) then green. P0's builder and document tests passed unchanged.
  - Full suite: 492 tests, 469 passed.
- **Task 5, pdffonts:** 4 tests, red (CS0117).
  - The first version of `Poppler.ListFonts` was written through a shell here-document, which turned its escape sequences into line breaks. It was rewritten with the file tools before any test ran, as the plan's constraint requires.
  - pdffonts and qpdf aren't installed locally, so three of the tests skip. The fourth, `ACharacterTheFontLacksStillCopiesOut`, ran against xpdf's pdftotext and passed: a character Noto Sans lacks copies out as itself.
  - Full suite: 496 tests, 470 passed and 26 skipped. The PR's CI run is where the pdffonts checks first run.
