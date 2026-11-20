# P1.1 TrueType parser: execution log

Issue [#20](https://github.com/Maviray/pdf/issues/20). Plan: [2026-11-20-issue-20-truetype-parser.md](../plans/2026-11-20-issue-20-truetype-parser.md).

The branch `feature/20-truetype-parser` is stacked on `docs/p1-fonts-spec` (PR #19), which holds the approved spec and was still waiting to be merged.

## 2026-11-20

- **Noto Sans.** The release `NotoSans-v2.015` was downloaded from `notofonts/latin-greek-cyrillic` with the GitHub CLI. The four static hinted TTFs and `OFL.txt` were extracted; the plan records their checksums.
- **Task 1, fonts and notices:** 5 tests, red (`FileNotFoundException`) then green.
  - Departure from the plan: `OFL.txt` has LF line endings, and a Windows checkout with `core.autocrlf=true` would turn them into CRLF and change its pinned checksum. `.gitattributes` now keeps that file at LF on every platform.
  - Full suite: 247 passed, 7 skipped.
- **Task 2, builder, reader and directory:** 12 tests, red (CS0234: the namespace didn't exist yet) then green.
  - The test font builder writes complete fonts with correct checksums.
  - Full suite: 259 passed.
- **Task 3, character map:** 22 tests, red (CS0246) then green.
  - Noto Sans's (3, 1) format 4 and (3, 10) format 12 subtables agree on every BMP code point, in all four styles.
  - Full suite: 281 passed.
- **Task 4, `TrueTypeFont`:** 52 tests, red (CS0246) then green.
  - The first green run had one failure, in the test: a glyph's `loca` range includes the builder's padding, so it is 36 bytes, not 34.
  - Three tests beyond the plan's list pin rules the plan states: `WithoutPostTheAngleAndUnderlineAreZero`, `ReadsTheItalicAngleAsFixedPoint` and `NamePrefersUsEnglishAndTypographicFamily`.
  - Full suite: 333 passed.
- **Task 5, composite components:** 17 tests, red (CS0103) then green.
  - Noto Sans "Ä" is "A" plus a mark in every style, and the mark's record carries instructions (`0x0107`).
  - Full suite: 350 passed.
- **Task 6, arbitrary bytes:** 2 tests. The red step was a mutant.
  - Without the `loca` bounds check, the byte-change sweep found 23 escapes (`ArgumentOutOfRangeException`), through the `glyf` record's length and `loca` itself.
  - With the check restored, every truncation, and each of the four values at every byte, either loads or throws a documented exception, so there was nothing to fix.
  - Full suite: 352 passed.
- **Task 7, fontTools:** 5 tests, red (CS0103).
  - Departure from the plan: the test helpers gained `ExternalTool.Find` (now internal), `ExternalTool.RunFile` and `GeneratedFiles.SaveAs`. `SaveAs` has its own name because `Save(bytes, ".ttf")` would bind the extension to the caller-file parameter.
  - fontTools isn't installed on the development machine, and installing it means fetching a package, so the five tests skip locally with the install message. The PR's CI run is where they first run.
  - Both workflows install `python3-fonttools` and print its version. `CONTRIBUTING.md` names fontTools and how to install it.
  - Full suite: 352 passed, 12 skipped.
- **First CI run** (PR #24, run 44702178625, at `d85e861`): 364 of 364 tests succeeded, with 0 skipped. CI's fontTools is 4.61.1, and our parser agrees with it on all four Noto Sans styles.
- **Whole-branch review** by a fresh reviewer: merge with fixes. Its own sweeps (random multi-byte damage, edge values in every field, shrunk table lengths, Noto's structural tables) found no exception type other than the two documented ones.
  - **Fixed:** with overlapping format 4 segments, which real fonts have, `CharacterMappings()` gave duplicates and glyphs that lookups don't give. A segment now owns only the codes after the previous segment's end. `Format4MappingsFollowLookupsWhenSegmentsOverlap` was red, then green.
  - **Fixed:** the damage sweep checked only exception types, so it had let that bug through. It now has a second starting font in format 12 with a `0x00C9` composite, checks each glyph on its own, and asserts that mappings are strictly ascending and agree with lookups. Before the fix it was red, catching the overlap at byte 309.
  - Full suite: 355 passed, 12 skipped.
  - **Deferred, minor:**
    - the `pip install` advice can leave the check skipped, because `-I` hides user installs;
    - a final `0xFFFF` segment with range offset `0xFFFF` is refused;
    - the permission check runs after `post`, not straight after `OS/2`;
    - a damaged `name` record the library never reads refuses the font;
    - the fontTools dump could cover the remaining Noto Sans values.
- **CI after the fixes** (run 44706823225, at `fc6c49d`): 367 of 367 tests succeeded, with 0 skipped.
