# P0.2 Streaming file writer, checked by qpdf and poppler: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A complete, valid PDF file with blank pages: header, indirect objects, cross-reference table, trailer and `/ID`, with Flate streams. The test suite checks every generated file with qpdf and poppler, and CI requires both tools.

**Architecture:** `PdfFileWriter` owns the file structure: object numbers, offsets, xref and trailer. `PdfDocumentWriter` sits on top of it and owns pages, the page tree and the catalog. The test project gets one helper per external tool, which runs that tool on a file the test saved. Everything in `src/` stays `internal` in `Maviray.Pdf.Writer`.

**Tech Stack:** .NET 10, C#, `System.IO.Compression.ZLibStream`, xUnit v3, qpdf, poppler-utils (`pdftoppm`, `pdftotext`), GitHub Actions on `ubuntu-latest`.

**Spec:** [P0 Foundations](../specs/2026-11-19-p0-foundations-design.md), decisions D-05 to D-08, D-11 and D-12. Work item [#10](https://github.com/Maviray/pdf/issues/10). Builds on P0.1 ([plan](2026-11-19-issue-9-pdf-object-model.md)).

## Global Constraints

- The P0.1 constraints still hold: `internal` types in `Maviray.Pdf.Writer`, one type per file, no package references in `src/`, no reflection, warnings as errors, explicit `\n` in expected bytes, LF line endings in every file the writer emits.
- C# that contains backslashes is written with the file tools, never through a shell here-document (see the P0.1 log).
- Compressed output is decompressed before comparing (D-11).
- A validator test calls `ExternalTool.Locate` (directly or through `Qpdf` and `Poppler`). It skips when the tool is missing, unless `MAVIRAY_PDF_REQUIRE_VALIDATORS` is `true`, which makes it fail. qpdf and pdftoppm aren't installed on the development machine, so the CI run of the pull request is where those tests are first seen running. That includes the negative self-tests, which prove the helpers can fail.
- No commits while tasks run. The change set is committed at the end, as one code commit and one docs commit.

## Review Focus

1. **Object offsets with a non-seekable destination.** Every xref offset must point at `n 0 obj` when the output is a `NonSeekableStream`. Pinned in Task 2.
2. **An unwritten or doubly written object.** `Close` must name the unwritten object numbers. A second `Write` to the same reference must throw before any byte is written. Pinned in Task 2.
3. **A reference from another writer.** Writing to a reference number the writer never reserved must throw. Pinned in Task 2.
4. **Pages out of order.** Starting a page while one is open, ending a page that isn't the open one, and closing with a page open must all throw. Pinned in Task 4.
5. **A validator that can't fail.** Each helper has a negative self-test on a broken file, so a helper that always passes is caught in CI. Pinned in Task 5.

---

### Task 1: Names for the file structure, and streams with a filter

**Files:**
- Modify: `src/Maviray.Pdf/Writer/PdfName.cs`: add shared names.
- Modify: `src/Maviray.Pdf/Writer/PdfStream.cs`: add `WriteEncoded`.
- Test: `tests/Maviray.Pdf.Tests/Writer/PdfStreamTests.cs`, `PdfNameTests.cs`

**Interfaces:**
- Produces:
  - Static `PdfName` instances: `Type`, `Page`, `Pages`, `Catalog`, `Kids`, `Count`, `Parent`, `MediaBox`, `Resources`, `Root`, `Size`, `Id` (value `"ID"`), `FlateDecode`.
  - `void PdfStream.WriteEncoded(PdfOutput output, ReadOnlySpan<byte> encodedData, PdfName? filter)`: writes the dictionary's entries, then `/Length <encodedData.Length>`, then `/Filter <filter>` when `filter` is not null, then the framing from D-08. `WriteTo` becomes `WriteEncoded(output, Data.Span, null)`. It runs the same `/Length`/`/Filter` check first.

- [ ] **Step 1: Write the failing tests**
  - `PdfStreamTests.WriteEncodedAddsTheFilterAfterTheLength`: a dictionary with `Type /XObject`, encoded data `"abc"`, and filter `PdfName.FlateDecode` write `"<</Type /XObject /Length 3 /Filter /FlateDecode>>\nstream\nabc\nendstream"`.
  - `PdfNameTests.SharedNamesWriteTheirValue`: also asserts `/Type /Page /Pages /Catalog /Kids /Count /Parent /MediaBox /Resources /Root /Size /ID /FlateDecode`.
- [ ] **Step 2: Run them to verify they fail.** Run: `dotnet test --project tests/Maviray.Pdf.Tests -c Release -- --filter-namespace "Maviray.Pdf.Tests.Writer"`. Expected: build error CS0117 or CS1061 (no `FlateDecode`, no `WriteEncoded`).
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run them to verify they pass.** Same command. Expected: `failed: 0`.
- [ ] **Step 5: Checkpoint.** `dotnet build -c Release` gives 0 warnings, and `dotnet test -c Release` passes.

### Task 2: `PdfWriterOptions` and `PdfFileWriter`

**Files:**
- Create: `src/Maviray.Pdf/Writer/PdfWriterOptions.cs`, `src/Maviray.Pdf/Writer/PdfFileWriter.cs`
- Create: `tests/Maviray.Pdf.Tests/Writer/PdfFileStructure.cs` (test helper)
- Test: `tests/Maviray.Pdf.Tests/Writer/PdfFileWriterTests.cs`

**Interfaces:**
- Consumes: `PdfOutput`, `PdfObject` and its subtypes, and `PdfName.Size`/`Root`/`Id` (P0.1, Task 1).
- Produces:
  - `internal sealed class PdfWriterOptions`
    - `CompressionLevel CompressionLevel { get; init; } = CompressionLevel.Optimal`
    - `byte[]? FileId { get; init; }`: must be exactly 16 bytes when set.
  - `internal sealed class PdfFileWriter : IDisposable`
    - `PdfFileWriter(Stream destination, PdfWriterOptions options)`: writes the header at once. Throws `ArgumentException` if `FileId` isn't 16 bytes.
    - `PdfReference Reserve()`
    - `void Write(PdfReference reference, PdfObject value)` and `PdfReference Write(PdfObject value)`
    - `void Close(PdfReference root)`: writes the xref table and the trailer, then flushes the output and the destination.
    - `long Position`: the bytes written so far.
    - Every member throws `InvalidOperationException` after `Close`. `Dispose` releases the output and never disposes the destination.
- Test helper produced: `PdfFileStructure` (static `Parse(byte[] file)`) returns:
  - `string Text`: Latin-1;
  - `long StartXref`;
  - `IReadOnlyList<long> Offsets`: index = object number, where index 0 is the free entry;
  - `string Trailer`: the text between `trailer\n` and `\nstartxref`.

  It asserts as it parses: every xref entry is exactly 20 bytes and ends with a space and LF, and the free entry is `0000000000 65535 f \n`.

- [ ] **Step 1: Write the failing tests** (`PdfFileWriterTests`):
  - `WritesTheHeaderFirst`: the file starts with `"%PDF-1.7\n%âãÏÓ\n"`.
  - `FramesEachIndirectObject`: the first object, `<<>>`, follows the header as `"1 0 obj\n<<>>\nendobj\n"`.
  - `EveryOffsetPointsAtItsObject`: three objects are written in the order 2, 1, 3 (reserve all three first). For each n, `Text` at `Offsets[n]` starts with `"{n} 0 obj\n"`, and `Text` at `StartXref` starts with `"xref\n0 4\n"`.
  - `OffsetsAreRightWithANonSeekableDestination`: the same check, written through `NonSeekableStream`.
  - `TrailerNamesSizeRootAndId`: with `FileId` = bytes 0..15 and root = object 1, the trailer is exactly `"<</Size 2 /Root 1 0 R /ID [<000102030405060708090A0B0C0D0E0F> <000102030405060708090A0B0C0D0E0F>]>>"`, and the file ends with `"startxref\n{StartXref}\n%%EOF\n"`.
  - `RandomIdsDifferBetweenFiles`: two files written without a `FileId` have different `/ID` values, and in each file the two halves are equal.
  - `RefusesAFileIdThatIsNot16Bytes`: 15 bytes throw `ArgumentException`.
  - `WritingAnObjectTwiceThrows`: the second `Write(ref, …)` throws `InvalidOperationException`, and `Position` hasn't changed.
  - `ClosingWithAnUnwrittenObjectNamesIt`: after reserving 1 and 2 and writing only 1, `Close` throws `InvalidOperationException` whose message contains `"2"`.
  - `RefusesAReferenceItNeverReserved`: `Write(new PdfReference(5), …)` on a writer that reserved 1 throws `InvalidOperationException`.
  - `RefusesWritesAfterClose`: `Reserve`, `Write` and `Close` throw `InvalidOperationException`.
  - `DisposeLeavesTheDestinationOpen`
- [ ] **Step 2: Verify they fail.** Run: `dotnet test --project tests/Maviray.Pdf.Tests -c Release -- --filter-class "Maviray.Pdf.Tests.Writer.PdfFileWriterTests"`. Expected: build error CS0246 (no `PdfFileWriter`).
- [ ] **Step 3: Implement.**
  - Offsets go in a `List<long>` indexed by object number, with `-1` meaning not yet written.
  - The xref offset is formatted as ten zero-padded digits. An offset of `10_000_000_000` or more throws `InvalidOperationException`.
  - `/ID` is two `PdfString`s in hex form over the same 16 bytes. Without a `FileId`, they come from `RandomNumberGenerator.Fill`.
- [ ] **Step 4: Verify they pass.** Same command. Expected: `failed: 0`, `total: 12`.
- [ ] **Step 5: Checkpoint.** Same as Task 1.

### Task 3: Flate compression of streams

**Files:**
- Modify: `src/Maviray.Pdf/Writer/PdfFileWriter.cs`
- Test: `tests/Maviray.Pdf.Tests/Writer/PdfFileWriterTests.cs`

**Interfaces:**
- Consumes: `PdfStream.WriteEncoded` (Task 1) and `PdfWriterOptions.CompressionLevel` (Task 2).
- Produces: `PdfFileWriter.Write` compresses a `PdfStream` value with `ZLibStream` and adds `/Filter /FlateDecode`. With `CompressionLevel.NoCompression`, it calls `WriteTo` unchanged.

- [ ] **Step 1: Write the failing tests:**
  - `CompressesStreamsWithFlate`: a stream holding 1,000 bytes of `"BT ET\n"` repeated.
    - The object text contains `/Filter /FlateDecode`.
    - `/Length` equals the byte count between `stream\n` and `\nendstream`.
    - Decompressing those bytes with `ZLibStream` gives the input back.
  - `WritesStreamsAsTheyAreWithoutCompression`: with `NoCompression`, the object holds the raw data and contains no `/Filter`.
- [ ] **Step 2: Verify they fail.** Run the Task 2 command. Expected: `CompressesStreamsWithFlate` fails, because there's no `/Filter`.
- [ ] **Step 3: Implement.** Compress into a `MemoryStream` with `leaveOpen: true`, then pass its buffer to `WriteEncoded`.
- [ ] **Step 4: Verify they pass.** Expected: `failed: 0`, `total: 14`.
- [ ] **Step 5: Checkpoint.** Same as Task 1.

### Task 4: `PdfDocumentWriter` with blank pages

**Files:**
- Create: `src/Maviray.Pdf/Writer/PdfDocumentWriter.cs`, `src/Maviray.Pdf/Writer/PdfPageWriter.cs`
- Test: `tests/Maviray.Pdf.Tests/Writer/PdfDocumentWriterTests.cs`

**Interfaces:**
- Consumes: `PdfFileWriter` and `PdfWriterOptions` (Task 2), and the names from Task 1.
- Produces:
  - `internal sealed class PdfDocumentWriter : IDisposable`
    - `PdfDocumentWriter(Stream output, PdfWriterOptions? options = null)`: reserves the page tree at once.
    - `PdfPageWriter BeginPage(double width, double height)`: throws `ArgumentOutOfRangeException` unless both are greater than 0 and finite.
    - `void EndPage(PdfPageWriter page)`: writes `<</Type /Page /Parent p 0 R /MediaBox [0 0 w h] /Resources <<>>>>`.
    - `void Close()`: writes the page tree `<</Type /Pages /Kids [...] /Count n>>`, then the catalog `<</Type /Catalog /Pages p 0 R>>`, then calls `PdfFileWriter.Close(catalog)`.
    - `Dispose()`
  - `internal sealed class PdfPageWriter`, with `double Width` and `double Height`. P0.3 adds `Content`.

- [ ] **Step 1: Write the failing tests:**
  - `WritesABlankA4Page`: `FileId` fixed, `NoCompression`, one 595.2756 × 841.8898 page.
    - Object 2 is `<</Type /Page /Parent 1 0 R /MediaBox [0 0 595.2756 841.8898] /Resources <<>>>>`.
    - Object 1 is `<</Type /Pages /Kids [2 0 R] /Count 1>>`.
    - Object 3 is `<</Type /Catalog /Pages 1 0 R>>`.
    - The trailer has `/Root 3 0 R`.
  - `KeepsPagesInOrder`: three pages give `/Kids [2 0 R 3 0 R 4 0 R] /Count 3`.
  - `ClosingWithNoPagesThrows`, `BeginningAPageWhileOneIsOpenThrows`, `EndingAPageThatIsNotOpenThrows` and `ClosingWithAPageOpenThrows`: each throws `InvalidOperationException`.
  - `RefusesPageSizesThatAreNotPositive`: a theory with (0, 10), (10, -1) and (NaN, 10), each throwing `ArgumentOutOfRangeException`.
- [ ] **Step 2: Verify they fail.** Run: `dotnet test --project tests/Maviray.Pdf.Tests -c Release -- --filter-class "Maviray.Pdf.Tests.Writer.PdfDocumentWriterTests"`. Expected: build error CS0246.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Verify they pass.** Expected: `failed: 0`, `total: 9`.
- [ ] **Step 5: Checkpoint.** Same as Task 1.

### Task 5: Validator helpers and the blank-page check

**Files:**
- Create in `tests/Maviray.Pdf.Tests/Validation/`, namespace `Maviray.Pdf.Tests.Validation`: `ExternalTool.cs`, `ToolResult.cs`, `Qpdf.cs`, `Poppler.cs`, `PpmImage.cs`, `GeneratedFiles.cs`
- Test: `tests/Maviray.Pdf.Tests/Validation/PpmImageTests.cs`, `ValidatorSelfTests.cs`, and `tests/Maviray.Pdf.Tests/Writer/BlankPageValidationTests.cs`

**Interfaces:**
- Produces:
  - `internal sealed record ToolResult(int ExitCode, string StandardOutput, string StandardError)`
  - `internal static class ExternalTool`
    - `string Locate(string tool)`: searches `PATH` for `tool`, and for `tool.exe` on Windows. A missing tool calls `Assert.Skip("<tool> is not installed. Install qpdf and poppler-utils to run this check.")`. When `MAVIRAY_PDF_REQUIRE_VALIDATORS` is `true` (ordinal, case-insensitive), it calls `Assert.Fail` instead.
    - `ToolResult Run(string tool, params string[] arguments)`: locates the tool and starts it with `ArgumentList`. It reads stdout and stderr asynchronously, waits up to 60 seconds, and on timeout kills the process tree and fails.
  - `internal static class Qpdf`, with `void Check(string path)`: runs `--check`. Any exit code other than 0 fails with the full output.
  - `internal static class Poppler`
    - `IReadOnlyList<PpmImage> Render(string path)`: deletes old `<path without extension>-*.ppm` files, then runs `pdftoppm -r 72 <path> <path without extension>`. It fails on a nonzero exit code or any stderr output. It returns the pages, sorted by the page number in the file name.
    - `string ExtractText(string path)`: runs `pdftotext -enc UTF-8 <path> -` and returns stdout with trailing whitespace and form feeds removed. A comment states that this hides differences in trailing whitespace and page breaks (D-11).
  - `internal sealed class PpmImage`, with `int Width`, `int Height`, `(byte R, byte G, byte B) GetPixel(int x, int y)`, and `static PpmImage Parse(byte[] bytes)`. It parses binary P6 with a maximum value of 255, and throws `FormatException` otherwise.
  - `internal static class GeneratedFiles`, with `string Save(byte[] pdf, [CallerFilePath] string file = "", [CallerMemberName] string name = "")`. It writes `AppContext.BaseDirectory/generated/<test file name>/<name>.pdf` and returns that path.

- [ ] **Step 1: Write the failing tests:**
  - `PpmImageTests.ParsesABinaryP6Image`: the bytes `"P6\n2 1\n255\n"` followed by `FF 00 00 00 00 FF` give a 2 × 1 image, with pixel (0,0) = (255,0,0) and pixel (1,0) = (0,0,255).
  - `PpmImageTests.RefusesOtherFormats`: `"P3\n…"` throws `FormatException`.
  - `ValidatorSelfTests.QpdfRejectsAFileWithAWrongStartxref`: a valid blank-page file with its `startxref` value replaced by `0` makes `Qpdf.Check` throw an `XunitException`. Call `ExternalTool.Locate("qpdf")` first, so a missing tool skips rather than satisfying the assertion.
  - `ValidatorSelfTests.PopplerRejectsAFileThatIsNotAPdf`: `"not a pdf"` makes `Poppler.Render` throw an `XunitException`. Locate `pdftoppm` first.
  - `ValidatorSelfTests.ExtractTextReadsNothingFromABlankPage`: `Poppler.ExtractText` on a blank page returns `""`.
  - `BlankPageValidationTests.BlankA4PassesQpdfAndRendersWhite`:
    - `Qpdf.Check` passes.
    - `Render` gives exactly one page.
    - Its width is 595 or 596 and its height is 841 or 842.
    - Every pixel is (255, 255, 255).
- [ ] **Step 2: Verify they fail.** Run: `dotnet test --project tests/Maviray.Pdf.Tests -c Release`. Expected: build error CS0246 (no `PpmImage`).
- [ ] **Step 3: Implement** the six helper types.
- [ ] **Step 4: Verify locally.** Expected:
  - the `PpmImage` tests and `ExtractTextReadsNothingFromABlankPage` pass, because `pdftotext` is installed here;
  - the qpdf and pdftoppm tests skip, with a message naming the tool.
- [ ] **Step 5: Checkpoint.** Same as Task 1.

### Task 6: CI and the contributor guide

**Files:**
- Modify: `.github/workflows/build.yml`, `.github/workflows/publish-nuget.yml`, `CONTRIBUTING.md`

- [ ] **Step 1: Add an install step to both workflows** before `Test`, named "Install the PDF validators":

  ```yaml
  run: |
    sudo apt-get update
    sudo apt-get install -y --no-install-recommends qpdf poppler-utils
    qpdf --version
    pdftoppm -v
  ```

  Then add `env: MAVIRAY_PDF_REQUIRE_VALIDATORS: true` to both `Test` steps.
- [ ] **Step 2: In `build.yml` only, add an upload step after `Test`:**

  ```yaml
  - name: Upload the generated PDFs
    if: always()
    uses: actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9 # v7.0.2
    with:
      name: generated-pdfs
      path: tests/**/bin/**/generated/
      if-no-files-found: warn
      retention-days: 14
  ```

- [ ] **Step 3: Add a "Running the tests" section to `CONTRIBUTING.md`:**
  - the tests check generated PDFs with qpdf and poppler when they're installed, and skip those checks otherwise;
  - CI installs both tools and requires them;
  - `MAVIRAY_PDF_REQUIRE_VALIDATORS=true` makes a missing tool fail locally too.
- [ ] **Step 4: Verify.** Run `git diff --check`. Expected: no output. Check the YAML indentation by eye against the existing steps.

### Task 7: Records and hand-back

- [ ] **Step 1:** Write `docs/releases/0.1.0/execution-logs/2026-11-19-issue-10-streaming-file-writer.md`, appending as tasks run.
- [ ] **Step 2:** Update the release index:
  - the P0.2 row becomes "In review", with links to this plan and the log;
  - the Layout section is unchanged.
- [ ] **Step 3:** Correct the spec's status line to "approved, 2026-11-19". The P0.1 review left this as a minor finding.
- [ ] **Step 4:** Verify.
  - Full build and suite.
  - `git status --short`: only the files named in this plan.
- [ ] **Step 5:** Commit, push, open the PR into `develop`, and confirm that CI ran every validator test with none skipped. Since the PR is stacked on #14, its description says to merge #14 first.
