# P1.8 Text gate: execution log

Issue [#33](https://github.com/Maviray/pdf/issues/33). Plan: [2026-11-24-issue-33-text-gate.md](../plans/2026-11-24-issue-33-text-gate.md).

The branch `feature/33-text-gate` is stacked on `feature/32-text-layout` (PR #42), which is stacked on `feature/31-line-breaking` (PR #41). #40 had merged, and both were refreshed onto develop the same morning.

## 2026-11-24

- **The validators, installed locally.** The user allowed installing test tools for this library. Until now qpdf, poppler and fontTools ran only in CI, and 28 checks were skipped locally.
  - qpdf 12.4.2 (`QPDF.QPDF`) and poppler 26.09.0 (`oschwartz10612.Poppler`) came from winget. qpdf's folder was added to the user's PATH, because its silent installer doesn't add it. fontTools 4.68.0 came from PyPI, into Python's own site-packages, because the check runs Python with `-I`, which ignores the per-user folder.
  - With `MAVIRAY_PDF_REQUIRE_VALIDATORS=true`, 801 of 803 tests passed and none were skipped. The two failures were the P0 Helvetica tests. The Windows build of poppler writes `Syntax Error: No display font for 'Symbol'` and `... for 'ArialUnicode'` for any font that isn't embedded, and the helper fails on anything on stderr.
- **Planning.** A throwaway probe laid out the draft gate text, painted it, and read it back with poppler. Its findings are the plan's four rulings:
  - the line height of 1.2, because the paragraphs at the normal 1.362 need 864 points;
  - pdftotext's `-layout` mode, because the default mode joined the soft hyphen's line to the next and dropped the U+00AD, and `-raw` dropped the narrow no-break space;
  - U+00A0 and U+202F mapped to U+0020 for the pdftotext comparison. qpdf's QDF form showed that the ToUnicode map is right, so poppler is what writes them as spaces.
  - the stderr filter for the Windows build.
  - The probe also fixed the strings. Shortened to 73 words, the prose lost its line end at the first soft hyphen, so a second went into `Kunden|nummer`, after `werktags`, where the 220-point fill breaks.
- **Task 1, the poppler filter:** `Poppler.Diagnostics` drops exactly the missing-display-font line, and the two P0 tests pass on Windows. The suite passed 803 of 803, with every validator required.
- **Task 2, the greedy check:**
  - It rebuilds the text with `TextNormalizer`, finds opportunities with `LineBreaker` and clusters with `StringInfo`, and measures each line from the fonts' advances.
  - Its four tests of wrong layouts each find the problem they name. 30 correct fills, the prose and the mixed spans in three alignments at five widths, give none.
  - **Mutant:** spacing after a line's last cluster fails the correct fills on `BEZAHLT ` at 60 points, by 0.5.
- **Task 3, the gate:**
  - It passed on its first run, with every validator required.
  - **Mutants**, each failing the check meant for it:
    - paragraph 1 filled at 230 fails the greedy check, with two lines that don't fit 220;
    - two expected lines swapped fail the text comparison;
    - pdftotext's default mode fails at index 8, where it joined `E-` to `Mail-Adresse`, before it reaches the soft hyphen's line;
    - paragraph 3 painted 20 points to the left leaves 1,506 pixels outside the boxes.
  - Without ICU, the text tests passed 298 of 298.
- **Task 4, the checklist:**
  - **Found while writing it:** at 220 points the mixed paragraph's underline sat whole on one line, so it neither crossed a line break nor ended before a hanging space, as the description said.
  - **Ruling:** `ist` joined the underlined span. The text and its lines don't change. The gate now asserts that the underline ends the third line at the line's width and starts the fourth at 0, and it failed on the old spans. The plan's table is amended.
  - The description was checked against the test's constants: the path, 72 points (2.54 cm), 220 points (7.76 cm), 72 + 160 points (8.18 cm), the first top at 36 points (1.27 cm), the last bottom at 800.8 points (28.25 cm), and line 11 of the first paragraph.
- **Whole-branch review** by a fresh reviewer: merge with fixes, none of them critical.
  - It ran the suite with every validator, rendered and read the page, and ran 9 mutants and 8 probes, in a worktree it removed afterwards. It installed nothing.
  - It confirmed that the check uses none of `PreparedParagraph`'s widths, `BreakOpportunities` or `Fill`, and that the checklist matches the render.
  - **Found while it ran:** `Poppler` had become a partial class for `[GeneratedRegex]`. CONTRIBUTING forbids partial classes, and the reviewer found it too.
  - **Fixed.** Each change to a test or to the check was seen failing first; the wording and the remarks are docs:
    - **The partial class.** It is now a plain static regex. A new source rule bans partial types in `src/` and `tests/`, and failed on `Poppler.cs`.
    - **Three rules of the check were unpinned:** a break that is no opportunity, a cut where the first segment fits, and a required break inside a line. No real fill reaches them, so two tests build their lines by hand, and the cut test now matches its own message. Each test fails under a mutant that removes its rule.
    - **No-break spaces weren't tested.** At the gate's widths every sequence sits mid-line, where a breakable space of the same advance gives the same lines. `NoBreakSpacesKeepTheirNeighboursOnOneLine` fills the prose at 100, 130 and 190 points, where each sequence reaches a line's end. The same prose with breakable spaces breaks inside them there, so the widths can't miss: with breakable prose it failed at `20 €`.
    - **A hanging letter-spaced space wasn't measured anywhere.** `BEZAHLT ` now carries its space. A mutant that gives hanging spaces their letter spacing survives the old spans and fails the check's narrower fills on the new.
    - **The checklist's positions weren't pinned.** The gate asserts the six tops, from 36 to 788.8 points, and the bottom at 800.8. With a gap of 9 points it failed.
    - **The checklist's wording:**
      - the snippet now names each file's `.txt`;
      - the Turkish, Russian and Greek sentences run on;
      - a viewer that joins line 11 to line 12 after dropping the U+00AD is the same allowed difference.
    - **The check's remarks** name its blind spots: the fit test extends a line by its next segment only, and a whole first segment taken without its hyphen is measured with it.
  - **Full suite:**
    - Release: 813 tests, all passed;
    - Debug: 813 tests, 812 passed and 1 skipped (the allocation test, Release only);
    - without ICU: the text tests passed 302 of 302.
  - **Deferred, minor:**
    - the check reports a correct line where T-09 takes a whole first segment without its hyphen (`Kunden` U+00AD `nummer` at 38.13 points). It is documented as a blind spot, and the gate has no such line;
    - the check doesn't report an empty line, and builds a line's name before it checks the range. The gate's text and ink checks catch an empty line.
  - **Rulings on what the reviewer set aside:**
    - T-07's soft-hyphen row and T-09's "No hyphen is drawn at such a break" read differently for a whole first segment. The engine and the check follow T-09, the more specific rule.
    - The Turkish, Russian and Greek sentences are only in Regular. T-15's "in three styles" is page-wide, and the fonts gate draws those scripts in all four styles.
    - The ToUnicode entries for U+00A0 and U+202F are checked by hand only. P1.3's CMap tests cover any code point, and the writer special-cases neither.
- **CI** on `c27b8da`, [run 45346056945](https://github.com/Maviray/pdf/actions/runs/45346056945): 813 of 813 tests passed, and without ICU the text tests passed 302 of 302. The gate ran there with Ubuntu's poppler and qpdf, which match the local Windows builds on every check.
