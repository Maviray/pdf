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
- **First CI run** (PR #17, run 44527036223, at `2840375`): 238 of 238 tests succeeded, with 0 skipped. CI's poppler `pdftotext` agrees with xpdf's.
- **The P0.3 review fixes** were merged in from `feature/11` (`0ede95b`). `ContentStreamBuilder` conflicted, and the resolution kept this item's builder and applied the same operand checks. CI: 246 of 246, with 0 skipped.

## Final review

A fresh reviewer found no critical issues and two important ones, fixed in one pass:

- **A failed text call changed the stream.** A `SetFont` with a bad size, or a `SetTextMatrix` with a bad operand, wrote, reserved and tracked before checking its numbers. Both now check first.
  - `AThrowingTextCallWritesNothing` failed before the fix.
  - The test also asserts that no font became current, and that nothing was reserved or written.
- **"`Tj` outside a text object" passed for the wrong reason.** No font had been set, so `ShowText` refused because of the missing font instead.
  - The theory now sets a font in an earlier text object, which is the realistic mistake, because the font survives `ET`.
  - It fails against the reviewer's mutant.

Minor findings left for later:
- two untested sequencing cases;
- a lone surrogate in the refusal theory;
- an emoji's error naming only its high surrogate;
- literal invisible characters in the source. The file tool turned the `\uXXXX` escapes into the characters themselves. Behaviour is unchanged.
- the soft hyphen and no-break space, which follow Annex D. To be recorded for P1's hyphenation.
