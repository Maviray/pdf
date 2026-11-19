# P0.2 Streaming file writer, checked by qpdf and poppler: execution log

Issue [#10](https://github.com/Maviray/pdf/issues/10). Plan: [2026-11-19-issue-10-streaming-file-writer.md](../plans/2026-11-19-issue-10-streaming-file-writer.md).

The branch `feature/10-streaming-file-writer` is stacked on `feature/9-pdf-object-model` (PR #14), which was still waiting to be merged when this work started.

## 2026-11-19

- **Task 1, shared names and `PdfStream.WriteEncoded`:** 2 tests changed or added, red then green. Full suite: 109 passed.
- **Task 2, `PdfWriterOptions` and `PdfFileWriter`:** 12 tests, red then green.
  - Departure from the plan: the ID pattern in the tests is a static `Regex`, not `[GeneratedRegex]`. `[GeneratedRegex]` needs a partial class, which the repository's one-type-per-file rule forbids.
  - Full suite: 121 passed.
- **Task 3, Flate compression:** 2 tests. `CompressesStreamsWithFlate` failed before the change, because there was no `/Filter`. The uncompressed case already passed, as the plan expected. Full suite: 123 passed.
- **Task 4, `PdfDocumentWriter` and `PdfPageWriter`:** 9 tests, red then green. Full suite: 132 passed.
- **Task 5, validator helpers:** 6 tests, red then green.
  - On this machine, the `PpmImage` tests ran and passed. The three tests that need qpdf or pdftoppm skipped, with messages naming the tool.
  - `ExtractTextReadsNothingFromABlankPage` also ran and passed, but only from Git Bash. The `pdftotext` found there is Git for Windows' xpdf 4.00, not poppler, and from PowerShell that test skips too. CI's poppler is the reference.
  - With `MAVIRAY_PDF_REQUIRE_VALIDATORS=true`, the same run fails and says why.
  - Full suite: 138 tests, 135 passed and 3 skipped.
- **Task 6, CI and `CONTRIBUTING.md`:**
  - Both workflows install qpdf and poppler-utils and require them.
  - The pull-request build uploads the generated PDFs as `generated-pdfs`.
  - `git diff --check` is clean, and the line endings are unchanged.
- **Task 7:** the spec's status line now says "approved". The P0.1 review had left that as a minor finding.
- **First CI run** (PR #15, run 44522087016, at `484e785`):
  - 138 of 138 tests succeeded, with 0 skipped.
  - qpdf 12.3.2 and pdftoppm 26.01.0 were installed.
  - The `generated-pdfs` artifact was uploaded.
  - This was the first run of the qpdf and pdftoppm tests, including both negative self-tests.

## Final review

A fresh reviewer checked `c3aede9..484e785`. It found no critical issues and three important ones. All three were fixed in one pass:

- **A failed write could still be closed into a corrupt file.** When writing an object threw part-way, the object still counted as written, so `Close` succeeded and wrote a file with a truncated object.
  - The writer is now faulted by any exception from `Write` or `Close`.
  - It refuses an undefined compression level up front.
  - `AFailedWriteFaultsTheWriter` and `RefusesAnUndefinedCompressionLevel` failed before the fix.
- **No test covered poppler's stderr check.** Poppler repairs a wrong `startxref`, exits with 0, and reports the damage on stderr. `PopplerRejectsADamagedFileItCanStillRender` asserts that the failure says "exited with 0", so only the stderr check can satisfy it. It runs in CI.
- **"Ending a page that isn't the open one" wasn't pinned.** `EndingAnotherPageWhileOneIsOpenThrows` fails when the page check is broken the way the reviewer broke it, and passes on the real code.

The minor findings are left for later, among them:
- a negative self-test for `ExtractText`;
- concrete Windows install commands in `CONTRIBUTING.md`;
- keeping deliberately broken fixtures out of the viewer artifact.
