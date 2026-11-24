# P1.11 Text element, styles and fonts: execution log

Issue [#36](https://github.com/Maviray/pdf/issues/36). Plan: [2026-11-24-issue-36-text-element-styles-fonts.md](../plans/2026-11-24-issue-36-text-element-styles-fonts.md).

The branch `feature/36-text-element-styles-fonts` is stacked on `feature/35-containers` (PR #45), and so on #44 to #41.

## 2026-11-24

- **Planning.** The plan's rulings:
  - the engine's `TextStyle` becomes `SpanStyle`, so the public `TextStyle` can take its name;
  - the fluent layer merges container default styles as it builds;
  - `Italic`, `Underline` and `Strikethrough` take false too;
  - the Text element's cursor is a line index;
  - diagnostics name the element with `Page n` first.
  - P1.10's review added one point: the Text element keeps its fill at an infinite width besides its last finite one, because a Row measures an auto item there and at its width, again and again.
- **Task 1, the rename:** word-bounded, over 14 files. The suite is its test, and it passed unchanged.
- **Task 2, `Color` and `FontWeight`:** `FromHex` reads `#RRGGBB` and `#RGB` in either case, and refuses anything else, naming `hex`.
- **Task 3, the font registry:**
  - CSS Fonts 4 weight matching, with one case per rule;
  - registration from bytes, a stream (not disposed) or a path;
  - duplicate faces refused, and families compared ignoring case;
  - eight threads registering at once lose nothing.
- **Task 4, text styles:** span over element over inherited over the built-in default. A span can undo an inherited underline, strikethrough or italic.
- **Task 5, diagnostics:** each is reported once per document, and only the render pass carries the sink. `LayoutEngine.Generate` takes the sink, and passes the render flag explicitly rather than inferring it from the canvas.
- **Task 6, the Text element:**
  - **Ruling:** the preparation is its own type, `PreparedText`, by one type per file.
  - **Mutants**, each failing the test meant for it:
    - lines drawn at their absolute top, instead of relative to the page part;
    - always refilling at infinity;
    - preparing once whatever the page numbers.
  - The first cache mutant didn't compile, because an unused field is an error, so it was written as an assignment.
- **Task 7, a multi-page paragraph in PDF:**
  - Page after page, the paragraph's lines are exactly those of one fill at the content's width, and every footer reads `Seite n von N`.
  - qpdf accepts the file, and pdftotext reads each page's lines back in order.
  - A mutant that draws the cursor's line again on the next page fails it.
  - At 30 repetitions the prose made only three pages, so it repeats 50 times.
- **Full suite:** Release, 1,039 tests, all passed, with every validator required.
- **Whole-branch review** by a fresh reviewer: not ready to merge, because of one critical finding.
  - It reproduced each finding in a worktree it removed afterwards, and installed nothing.
  - It confirmed the public surface, the registry's thread safety, every rule of the weight matching, the once-per-document diagnostics and the fill cache.
  - **Fixed. Each test was seen failing first, on the old element or under a mutant:**
    - **A multi-page Text lost or repeated lines when drawn at another width than it was measured at.** Plan ruling 4 kept the cursor as a line index, on the premise that a paragraph keeps its width across pages. But an Align, a Row's Auto item and a widening Flex item all draw a Text at another width, and T-09's round trip holds only at the whole fill's widest line.
      - Under an Align, page 2 drew `lange` instead of `kurz a`.
      - In an Auto item, the Text threw "a character is 2.58 pt wide and the space is 0 pt" for content that fits.
      - Beside a narrowing Auto column, words 12 to 24 were never drawn.
      - **The fix:** the cursor is a position in the text, kept as a span and an offset so it survives a page number that changes length. Each page part fills what is left from it, with `PreparedParagraph.Fill(width, from)`, and T-14 is amended.
      - The reviewer's probes are now tests, and four of them fail on the old element.
    - **Seven behaviours had no test.** Each now has one that fails under the reviewer's mutant:
      - the too-wide check counting only lines that fit;
      - preparing again at an infinite width;
      - letter spacing in em;
      - the element's style over the inherited one;
      - underline and strikethrough;
      - the height tolerance;
      - the width that `None` needs.
      - The first height pin, 7 lines of 12.3 at 86.1, survived its mutant. It was replaced by a space a hair short of the measured height.
    - `FontRegistry`'s public constructor and its exceptions are documented. `Diagnostic.PageNumber` says it is the page where the element was first drawn.
  - **Full suite:**
    - Release: 1,056 tests, all passed;
    - Debug: 1,056 tests, 1,055 passed and 1 skipped;
    - without ICU: the text tests passed 306 of 306.
  - **Rulings on what the reviewer set aside:** each is negligible, out of scope, or P1.12's or P1.13's:
    - missing glyphs after a later re-preparation;
    - `RegisterNotoSans` twice throwing;
    - fonts registered mid-generation;
    - the contract's 10⁻⁹ against the 10⁻⁶ tolerance;
    - an Align refilling twice;
    - the element's construction checks;
    - the stream copied twice;
    - `ToUpperInvariant` for families;
    - `TextStyle` having no value equality.
  - **For P1.12:**
    - builders construct a Text element only after its span calls finish, because `.Text("x").Bold()` styles the span after `.Text` returns;
    - P1.12 decides whether the text call gets an element-level style.
- **CI** on `bad13b8`, [run 45363300950](https://github.com/Maviray/pdf/actions/runs/45363300950): 1,059 of 1,059 tests passed, and without ICU the text tests passed 306 of 306.
