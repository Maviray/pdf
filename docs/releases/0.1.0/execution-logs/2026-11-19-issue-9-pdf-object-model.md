# P0.1 PDF object model and serialization: execution log

Issue [#9](https://github.com/Maviray/pdf/issues/9). Plan: [2026-11-19-issue-9-pdf-object-model.md](../plans/2026-11-19-issue-9-pdf-object-model.md). Branch `feature/9-pdf-object-model`, cut from `develop` at `17b53ab`.

## 2026-11-19

- **Task 1, `PdfOutput` and `InternalsVisibleTo`:** 11 tests, red then green. The red build failed with CS0234 (the `Maviray.Pdf.Writer` namespace didn't exist yet) rather than CS0246, which has the same cause. Full suite: 13 passed, 0 warnings.
- **Task 2, `PdfNumberFormat` and `WriteNumber`:** 21 tests, red then green. Departure from the plan: xUnit's analyzer treats `InlineData(0.0)` and `InlineData(-0.0)` as duplicates (xUnit1025), so the negative-zero case is its own test, `NegativeZeroWritesZero`. That test checks that its input really is negative zero. It was written after the code, so it was checked by disabling the `-0` fix: it failed with `"-0"`, and passed again once the fix was restored. Full suite: 34 passed, 0 warnings.
- **Task 3, `PdfObject` and the scalar types:** 16 tests, red then green. A refactor beyond the plan: `PdfReal` and `PdfNumberFormat` share one range check, `PdfNumberFormat.ThrowIfOutOfRange`, instead of each having a copy. Full suite: 50 passed, 0 warnings.
- **Task 4, `PdfName`:** 19 tests, red then green. Full suite: 69 passed, 0 warnings.
- **Task 5, `PdfString` and `PdfStringForm`:** 16 tests, red then green.
  - A refactor beyond the plan: hex digits are written by one method, `PdfOutput.WriteHex`, which `PdfName` and `PdfString` share.
  - The first version of `PdfString.cs` was written through a shell here-document, which collapsed doubled backslashes. The build caught it, and the file was rewritten.
  - Full suite: 85 passed, 0 warnings.
- **Task 6, `PdfArray`, `PdfDictionary` and `PdfStream`:** 16 tests, red then green.
  - Departures from the plan: `PdfArray` has one constructor taking `params`, which also serves `new PdfArray()`.
  - One test beyond the plan, `AddAppendsAtTheEnd`, covers `PdfArray.Add`.
  - Full suite: 101 passed, 0 warnings.

## Final review

A fresh reviewer checked the whole change set. It found no critical issues and three important ones. All three were fixed in one pass, and each fix started from a test that failed first.

- **Rounding.** `Math.Round(value, 4, AwayFromZero)` rounds the binary value, not the decimal one.
  - It rounded about 6% of decimal midpoints toward zero: `0.00015` gave `0.0001`.
  - It gave different bytes on .NET 11.
  - The format string now does the rounding, and `-0` is corrected to `0`.
  - New cases: `0.00015` → `0.0002`, `0.00145` → `0.0015`, `-0.00015` → `-0.0002`, `50.00005` → `50.0001`.
  - The first three failed before the fix.
- **Stream check.** `/Length` or `/Filter` set on a stream's dictionary after the stream was created was written as a duplicate key. `PdfStream.WriteTo` now checks again and throws `InvalidOperationException`. New test: `RefusesToWriteWhenLengthOrFilterIsSetLater`.
- **Buffer edge.** No test covered multi-byte writes that cross the buffer edge. The new test, `MultiByteWritesCrossTheBufferEdgeInOrder`, fails when the buffer check is broken the way the reviewer broke it, and passes on the real code.

Minor findings that were left for later:
- a name can contain a NUL byte;
- a stream can be put inside an array or dictionary, though streams must be indirect objects;
- a few behaviours with no test pinning them;
- the spec's status line still says "draft for approval".

## Result

- **Tests:** 106 new tests in `Maviray.Pdf.Tests.Writer`, plus the two existing `CoreAssemblyTests`. All 108 pass.
- **Build:** the Release build has no warnings, with warnings treated as errors.
- **Code:** every type is `internal`, in `src/Maviray.Pdf/Writer/`, one type per file.
