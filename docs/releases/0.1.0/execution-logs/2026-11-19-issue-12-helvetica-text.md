# P0.4 Helvetica text: execution log

Issue [#12](https://github.com/Maviray/pdf/issues/12). Plan: [2026-11-19-issue-12-helvetica-text.md](../plans/2026-11-19-issue-12-helvetica-text.md).

The branch `feature/12-helvetica-text` is stacked on `feature/11-content-stream-builder` (PR #16), on top of PRs #15 and #14. All three were still waiting to be merged.

## 2026-11-19

- **Task 1, `WinAnsiEncoding`:** 36 tests, red then green.
  - An exhaustive test over all 65,536 UTF-16 code units confirms the table encodes exactly 218 characters.
  - None of them maps to an undefined code (`0x81`, `0x8D`, `0x8F`, `0x90`, `0x9D`), to `0x7F`, or to a control code.
  - Full suite: 211 passed.
- **Task 2, `StandardFont` and `FontResources`:** 3 tests, red then green.
  - A shared `PdfName.Encoding` would hide `System.Text.Encoding` inside `PdfName`, so its constructor now uses the qualified name.
  - Full suite: 214 passed.
- **Task 3, text operators:** 19 tests, red then green.
  - Departure from the plan: the builder keeps its parameterless constructor alongside `ContentStreamBuilder(FontResources)`. P0.3's 25 graphics tests use it unchanged, and setting a font on such a builder throws.
  - `ShowText` encodes the text before writing anything, so an unencodable character leaves the stream unchanged.
  - Full suite: 233 passed.
- **Task 4, fonts in the page resources:** 2 tests, red then green.
  - The font object is reserved when a page first sets it, and written once at `Close`.
  - Each page lists only the fonts it used.
  - Full suite: 235 passed.
- **Task 5, text extraction:** 3 tests. The plan ordered this task after the resources it depends on, so its red step was a mutant instead.
  - From Git Bash, the two `pdftotext` tests run with Git for Windows' xpdf, and both pass.
  - With the page's `/Font` resource removed, both fail with empty output.
  - The qpdf test skips locally. The PR's CI runs all three with poppler.
