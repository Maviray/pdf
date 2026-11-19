# P0.3 Content-stream builder, with rectangles: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Pages get content. A content-stream builder writes the graphics operators for filled and stroked rectangles, skips redundant state changes, and refuses any operator order the PDF specification doesn't allow. Rectangles render where, and in the colour, the tests expect.

**Architecture:**
- `ContentStreamBuilder` writes operators into a per-page buffer, through a `PdfOutput` over a `MemoryStream`, so it reuses the shared number format.
- It tracks fill colour, stroke colour and line width, with a stack for `q`/`Q`, and tracks whether a path is open.
- `PdfDocumentWriter.EndPage` asks the builder for its finished bytes. It writes them as a compressed `PdfStream`, then the page dictionary with `/Contents`, and releases the buffer.
- The coordinates are PDF user space: points, with the origin at the bottom left (D-09).

**Tech Stack:** .NET 10, C#, xUnit v3, qpdf and pdftoppm in CI.

**Spec:** [P0 Foundations](../specs/2026-11-19-p0-foundations-design.md), decision D-09 (the graphics operators) and D-06 (`/Contents`). Work item [#11](https://github.com/Maviray/pdf/issues/11). Builds on P0.2 ([plan](2026-11-19-issue-10-streaming-file-writer.md)).

## Global Constraints

- The P0.1 and P0.2 constraints still hold. Every operator line ends with LF, and operands are separated by single spaces.
- The text operators (`BT`, `ET`, `Tf`, `Tm`, `Tj`) and the sequencing rules that involve them belong to P0.4. This plan leaves room for them, but doesn't add them.
- A tracked setting starts unknown, so the first setting on a page is always written, even black or a width of 1 (D-09).
- No commits while tasks run. At the end: one code commit and one docs commit.

## Review Focus

1. **`Q` restoring tracked state.** After `q`, a change, then `Q`, setting the value that was current before `q` must write nothing. Setting the changed value again must write it. Pinned in Task 2.
2. **Several rectangles in one path.** `re re f` is legal and must not throw. Pinned in Task 2.
3. **State changes inside an open path.** A colour, a line width, `cm`, `q` or `Q` between `re` and the painting operator must throw. Pinned in Task 2.
4. **A page with an open path or an open `q`.** `EndPage` must throw. Pinned in Task 3.
5. **Image coordinates.** The pixel checks must flip the y axis, because the render's origin is at the top left. A check that forgets this samples the wrong place and can still pass by accident on a symmetric layout, so the layout here is asymmetric. Pinned in Task 4.

---

### Task 1: `RgbColor`

**Files:**
- Create: `src/Maviray.Pdf/Writer/RgbColor.cs`
- Test: `tests/Maviray.Pdf.Tests/Writer/RgbColorTests.cs`

**Interfaces:**
- Produces: `internal readonly record struct RgbColor`, with a constructor `RgbColor(double red, double green, double blue)` and properties `Red`, `Green`, `Blue`. Each component must be between 0 and 1 inclusive; otherwise, including NaN, the constructor throws `ArgumentOutOfRangeException`.

- [ ] **Step 1: Write the failing tests:**
  - `AcceptsComponentsFromZeroToOne`: (0, 0.5, 1) keeps its values.
  - `RefusesComponentsOutsideZeroToOne`: a theory with (-0.1, 0, 0), (0, 1.1, 0) and (0, 0, NaN), each throwing `ArgumentOutOfRangeException`.
  - `EqualColorsAreEqual`: (0.2, 0.4, 0.8) equals (0.2, 0.4, 0.8).
- [ ] **Step 2: Verify they fail.** Run: `dotnet test --project tests/Maviray.Pdf.Tests -c Release -- --filter-class "Maviray.Pdf.Tests.Writer.RgbColorTests"`. Expected: build error CS0246.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Verify they pass.** Expected: `failed: 0`, `total: 5`.
- [ ] **Step 5: Checkpoint.** `dotnet build -c Release` gives 0 warnings, and `dotnet test -c Release` passes.

### Task 2: `ContentStreamBuilder` with the graphics operators

**Files:**
- Create: `src/Maviray.Pdf/Writer/ContentStreamBuilder.cs`. The tracked values are a small private nested `record struct GraphicsState(RgbColor? Fill, RgbColor? Stroke, double? LineWidth)`, which the one-type-per-file rule allows for a private helper used only inside its class.
- Test: `tests/Maviray.Pdf.Tests/Writer/ContentStreamBuilderTests.cs`

**Interfaces:**
- Consumes: `PdfOutput`, `RgbColor`.
- Produces: `internal sealed class ContentStreamBuilder : IDisposable`. Each operator method returns the builder for chaining.
  - `SaveState()` writes `q`, and `RestoreState()` writes `Q`.
  - `Transform(double a, double b, double c, double d, double e, double f)` writes `cm`.
  - `SetLineWidth(double width)` writes `w`.
  - `SetFillColor(RgbColor color)` writes `rg`, and `SetStrokeColor(RgbColor color)` writes `RG`.
  - `Rectangle(double x, double y, double width, double height)` writes `re`.
  - `Fill()` writes `f`, `Stroke()` writes `S`, and `FillAndStroke()` writes `B`.
  - `bool IsEmpty`: true when nothing has been written.
  - `byte[] Finish()`: throws `InvalidOperationException` if a path or a `q` is open. Otherwise it returns the bytes. The builder can't be used afterwards.
  - `Dispose()` releases the buffer.

- [ ] **Step 1: Write the failing tests.** Each test checks the exact bytes `Finish()` returns:
  - `WritesEachOperator`, a theory over the operators:
    - `SaveState().RestoreState()` gives `"q\nQ\n"`.
    - `Transform(1, 0, 0, 1, 72, 72)` gives `"1 0 0 1 72 72 cm\n"`.
    - `SetLineWidth(0.5)` gives `"0.5 w\n"`.
    - `SetFillColor(new(0.2, 0.4, 0.8))` gives `"0.2 0.4 0.8 rg\n"`.
    - `SetStrokeColor(new(1, 0, 0))` gives `"1 0 0 RG\n"`.
    - `Rectangle(72, 600, 200, 100).Fill()` gives `"72 600 200 100 re\nf\n"`.
    - With `Stroke()` it ends `"S\n"`, and with `FillAndStroke()` it ends `"B\n"`.
  - `WritesTheFirstSettingEvenIfItIsTheDefault`: `SetLineWidth(1)` gives `"1 w\n"`, and `SetFillColor(new(0, 0, 0))` gives `"0 0 0 rg\n"`.
  - `SkipsASettingThatIsAlreadyCurrent`:
    - `SetFillColor(red)` twice gives one `rg`.
    - `SetLineWidth(2)` twice gives one `w`.
    - `SetStrokeColor(blue)` twice gives one `RG`.
    - Fill and stroke are tracked separately: `SetFillColor(red).SetStrokeColor(red)` writes both.
  - `RestoreStateRestoresTheTrackedValues`: `SetFillColor(red).SaveState().SetFillColor(blue).RestoreState().SetFillColor(red).SetFillColor(blue)` gives `"1 0 0 rg\nq\n0 0 1 rg\nQ\n0 0 1 rg\n"`. The `red` after `Q` is already current, so it writes nothing.
  - `AllowsSeveralRectanglesInOnePath`: `Rectangle(0,0,1,1).Rectangle(2,2,1,1).Fill()` gives `"0 0 1 1 re\n2 2 1 1 re\nf\n"`.
  - `RestoreWithoutSaveThrows`, and `PaintingWithoutAPathThrows`, a theory over `Fill`, `Stroke` and `FillAndStroke`. Both throw `InvalidOperationException`.
  - `ChangingStateInsideAPathThrows`: a theory over `SetFillColor`, `SetStrokeColor`, `SetLineWidth`, `Transform`, `SaveState` and `RestoreState`, each called after `Rectangle` and before painting. Each throws `InvalidOperationException`.
  - `FinishingWithAnOpenPathOrStateThrows`: `Rectangle(…)` then `Finish()` throws, and so does `SaveState()` then `Finish()`.
  - `IsEmptyUntilSomethingIsWritten`
- [ ] **Step 2: Verify they fail.** Run: `dotnet test --project tests/Maviray.Pdf.Tests -c Release -- --filter-class "Maviray.Pdf.Tests.Writer.ContentStreamBuilderTests"`. Expected: build error CS0246.
- [ ] **Step 3: Implement.**
  - The tracked values are nullable (unknown), kept in a stack of `GraphicsState`.
  - An `_pathOpen` flag is set by `re` and cleared by painting.
  - Every operand goes through `PdfOutput.WriteNumber`.
- [ ] **Step 4: Verify they pass.** Expected: `failed: 0`.
- [ ] **Step 5: Checkpoint.**

### Task 3: Page content in `PdfDocumentWriter`

**Files:**
- Modify: `src/Maviray.Pdf/Writer/PdfPageWriter.cs`: add `ContentStreamBuilder Content { get; }`.
- Modify: `src/Maviray.Pdf/Writer/PdfDocumentWriter.cs`: `EndPage` writes the content stream, then the page with `/Contents`.
- Modify: `src/Maviray.Pdf/Writer/PdfName.cs`: add `Contents`.
- Test: `tests/Maviray.Pdf.Tests/Writer/PdfDocumentWriterTests.cs`

**Interfaces:**
- Consumes: `ContentStreamBuilder.Finish()` and `IsEmpty` (Task 2).
- Produces:
  - A page whose content isn't empty gets its content stream as the next object, then its page dictionary with `/Contents <n> 0 R` as the last key.
  - A page with empty content keeps the P0.2 dictionary, with no `/Contents`.
  - A page whose content is unfinished makes `EndPage` throw `InvalidOperationException`, and nothing is written.

- [ ] **Step 1: Write the failing tests:**
  - `WritesTheContentStreamBeforeThePage`: with `NoCompression`, a page with `Rectangle(72, 600, 200, 100).Fill()`.
    - Object 2 is `"2 0 obj\n<</Length 23>>\nstream\n72 600 200 100 re\nf\n\nendstream\nendobj\n"`.
    - Object 3 is the page dictionary, ending `/Resources <<>> /Contents 2 0 R>>`.
  - `EndingAPageWithAnOpenPathThrows`: `Rectangle` and no paint, then `EndPage` throws `InvalidOperationException`.
  - `WritesABlankA4Page` still passes unchanged.
- [ ] **Step 2: Verify they fail.** Expected: build error CS1061 (no `Content`).
- [ ] **Step 3: Implement.** `EndPage` calls `Finish()` first, so an unfinished page throws before anything is written. It disposes the builder after writing.
- [ ] **Step 4: Verify they pass.**
- [ ] **Step 5: Checkpoint.**

### Task 4: Rectangles in the render

**Files:**
- Test: `tests/Maviray.Pdf.Tests/Writer/RectangleRenderingTests.cs`

**Interfaces:**
- Consumes: `GeneratedFiles`, `Qpdf` and `Poppler` (P0.2), `PdfDocumentWriterTests.Write` with `CompressionLevel.Optimal`.

- [ ] **Step 1: Write the test** `FilledAndStrokedRectanglesRenderWhereExpected`. On an A4 page:
  - a blue rectangle, filled: `SetFillColor(new(0, 0, 1)).Rectangle(72, 600, 200, 100).Fill()`;
  - a red rectangle, stroked: `SetStrokeColor(new(1, 0, 0)).SetLineWidth(4).Rectangle(300, 200, 100, 150).Stroke()`.

  Then:
  - `Qpdf.Check` passes, and `Render` returns one page.
  - A helper converts PDF coordinates to pixels: `x` stays, and `y` becomes `page.Height - y`.
  - The expected pixels:
    - (172, 650) is (0, 0, 255): the middle of the blue rectangle.
    - (300, 275) is (255, 0, 0): the left edge of the red rectangle, at the middle of its stroke.
    - (350, 275) is white: inside the red rectangle, which is stroked only.
    - (50, 50) and (500, 750) are white.
  - The layout is asymmetric, so a missing y flip samples white where blue is expected and fails.
- [ ] **Step 2: Run it.**
  - Locally it skips, because qpdf and pdftoppm aren't installed. Its first run is in the PR's CI.
  - The asymmetric layout pins the y flip: the unflipped point for the blue rectangle, (172, 192), is white. So a missing flip fails the blue assertion. Record this in the log.
  - Poppler reports any content-stream syntax error on stderr (P0.2 review), and `Poppler.Render` fails on that. So a malformed operator also fails this test.
- [ ] **Step 3: Checkpoint.** CI must show the test run, with none skipped.

### Task 5: Records

- [ ] Write the execution log, update the release index row for #11, verify the change set, commit, push, and open the PR into `develop`.
