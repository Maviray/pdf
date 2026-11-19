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
  - On this machine, the `PpmImage` tests and `ExtractTextReadsNothingFromABlankPage` ran and passed, because `pdftotext` is installed here. The three tests that need qpdf or pdftoppm skipped, with messages naming the tool.
  - With `MAVIRAY_PDF_REQUIRE_VALIDATORS=true`, the same run fails and says why.
  - Full suite: 138 tests, 135 passed and 3 skipped.
- **Task 6, CI and `CONTRIBUTING.md`:**
  - Both workflows install qpdf and poppler-utils and require them.
  - The pull-request build uploads the generated PDFs as `generated-pdfs`.
  - `git diff --check` is clean, and the line endings are unchanged.
- **Task 7:** the spec's status line now says "approved". The P0.1 review had left that as a minor finding.
