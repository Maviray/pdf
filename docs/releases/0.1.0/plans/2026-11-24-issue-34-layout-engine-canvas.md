# P1.9 Layout engine and canvas: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The element protocol, the paging loop with its passes, and the canvases, of L-03 to L-05 and L-10. A test tree of fixed blocks pages correctly, and every error names its element.

**Architecture:**
- **The protocol.** An element is measured, then drawn, and reset before a pass. A measurement carries the element it comes from, so an error can name the innermost element that can't fit, or that split without advancing.
- **The engine.** It runs each page set's pages: background, header, content and footer. It runs the layout passes on the null canvas when the tree shows a page count, then the render pass on the canvas it is given.
- **The canvases.** The null canvas draws nothing. The PDF canvas maps the top-left, y-down canvas onto `ContentStreamBuilder`, and embeds each font once. The recording canvas, for tests, records every call at its absolute position.

**Tech Stack:** .NET 10, C#, xUnit v3; qpdf and poppler for the PDF canvas's page.

**Spec:** [P1 Layout and fluent API](../specs/2026-11-23-p1-layout-design.md), decisions L-03, L-04, L-05 and L-10, with L-01 and L-02 for layers and names. Work item [#34](https://github.com/Maviray/pdf/issues/34). The branch `feature/34-layout-engine-canvas` is stacked on `feature/33-text-gate` (PR #43).

## Global Constraints

- Every P0 and P1 constraint holds:
  - no runtime dependencies and no native code;
  - trimming- and AOT-safe, with no reflection;
  - `net10.0` only;
  - one type per file, no partial classes, and tests first.
- **Layers (L-01):**
  - the protocol and the engine in `src/Maviray.Pdf/Layout/` (`Maviray.Pdf.Layout`);
  - the canvases in `src/Maviray.Pdf/Drawing/` (`Maviray.Pdf.Drawing`);
  - every type `internal`, except `LayoutException`, which L-02 makes public. It lives in `src/Maviray.Pdf/Fluent/`, namespace `Maviray.Pdf`, with XML docs on every public member.
- Dependencies point downward: layout → drawing → writer, and layout → text.
- All lengths are `double`, in points.
- Tests mirror the folders: `tests/Maviray.Pdf.Tests/Layout/` and `tests/Maviray.Pdf.Tests/Drawing/`. Test elements and the recording canvas are test code, in those folders.
- Each task ends with one commit, subject `#34 <summary>`. The records are the last commit.

## Rulings made while planning

1. **A measurement carries its source.** L-05 leaves to the builder how the engine finds the innermost element.
   - `None` carries the innermost element that can't fit, and the size it needs when known. A container that is `None` because a child is passes the child's source up.
   - `Partial` carries the innermost element that splits.
   - **Cost if wrong:** containers in P1.10 must pass the source up, or errors name the container instead of the leaf.
2. **`Draw` returns whether it advanced.** It is how the engine sees L-04's "a Draw after Partial that advances nothing". A container advanced if any child did.
   - *Amended after the review:* a draw after `Full` always advances, because it finishes the element, and a container advanced if a child advanced or its own cursor moved. A measurement also carries the space its source had, which errors report.
3. **The content's `None` always throws.** The content is measured once per page, before anything of it is drawn there, so a `None` always meets L-04's empty-page rule. "Otherwise a new page follows" can't arise while a page set's pages all give the content the same space.
4. **The pass-limit error isn't about one element or one space.** Its `ElementPath` is `Document`, and its available sizes are `NaN`.
5. **The canvas keeps the translation itself** and writes absolute coordinates, so "translate (saved and restored)" needs no `q`/`Q` in the content stream.

## Review Focus

1. **An infinite loop.** A content that never fits, or that splits without advancing, must throw on the page where it happens. A test element for each.
2. **The header and footer are reset after every page,** so a header that is a splitting element draws whole on every page.
3. **The passes.**
   - The first layout pass shows the current page number.
   - Each later pass shows the count the one before ended with.
   - The render pass shows the final count, after `Reset()` on the whole tree.
   - Without a page-count element there is exactly one pass.
4. **The y flip.** A rectangle at the canvas's top left lands at the page's top left. A text line's baseline is its top plus the line's baseline.
5. **Paths.**
   - `Page n` comes first, and counts through the whole document across page sets.
   - Slots are named `Header`, `Content` and `Footer`.
   - The innermost element is the one named.

---

### Task 1: The protocol and `LayoutException`

**Files:**
- Create: `src/Maviray.Pdf/Layout/Fit.cs`, `Size.cs`, `Measurement.cs`, `Element.cs`, `PageContext.cs`
- Create: `src/Maviray.Pdf/Fluent/LayoutException.cs`
- Test: `tests/Maviray.Pdf.Tests/Layout/MeasurementTests.cs`, `ElementTests.cs`, `LayoutExceptionTests.cs`

**Interfaces:**
- Produces:
  - `enum Fit { Full, Partial, None }`.
  - `readonly record struct Size(double Width, double Height)`.
  - `readonly struct Measurement`, made only through factories:
    - `Full(Size, Element source)` and `Partial(Size, Element source)`;
    - `None(Element source, Size? needed = null)`.
    - Its properties are `Fit`, `Size` (zero for `None`), `Source` and `Needed`.
  - `sealed class PageContext`:
    - `int PageNumber`;
    - `int PageCount`, what a page-count span shows on this page;
    - `bool IsRenderPass`.
  - `abstract class Element`, whose constructor takes the element's `string name`:
    - `Name`, and `Parent`, which is null for a slot;
    - `Path`: the names from the slot down, joined with ` > `;
    - `abstract Measurement Measure(Size available, PageContext page)`;
    - `abstract bool Draw(Size available, ICanvas canvas, PageContext page)`: whether it advanced;
    - `abstract void Reset()`;
    - `virtual bool ShowsPageCount`: false; a container returns whether any child does;
    - `protected T Adopt<T>(T child) where T : Element`, which sets the child's `Parent` and throws `InvalidOperationException` when it has one already.
  - `public sealed class LayoutException : Exception`:
    - `string ElementPath`, `double AvailableWidth` and `double AvailableHeight`;
    - an internal constructor `(string elementPath, string reason, double availableWidth, double availableHeight)`, whose message is `"<path>: <reason>"`;
    - the standard constructors, if the analyzers require them: path empty, sizes NaN.
  - `internal static class LayoutErrors`, in `Layout/`, which formats lengths for messages as invariant `0.##` with ` pt`, and sizes as `<w> × <h> pt`.

- [ ] **Step 1: Test.**
  - `MeasurementTests`:
    - `NoneCarriesNoSize`;
    - `NoneKeepsWhatTheElementNeeds`;
    - `EachFactoryKeepsItsSource`.
  - `ElementTests`, with a test element:
    - `APathRunsFromTheSlotDown`: `Content > Stack > Block`;
    - `AdoptingAChildTwiceThrows`.
  - `LayoutExceptionTests`:
    - `TheMessageNamesThePathAndTheReason`: `Page 3 > Content > Block: it is 31.2 pt wide and the space is 20 pt.`;
    - `ItKeepsTheAvailableSize`;
    - `LengthsAreWrittenWithoutCulture`: under `de-DE`, 31.25 is `31.25 pt`.
- [ ] **Step 2:** Run them; they fail to compile.
- [ ] **Step 3:** Implement the types above.
- [ ] **Step 4:** Run them, then the suite. Commit `#34 add the element protocol and LayoutException`.

### Task 2: The canvases

**Files:**
- Create: `src/Maviray.Pdf/Drawing/ICanvas.cs`, `NullCanvas.cs`, `PdfCanvas.cs`
- Create: `tests/Maviray.Pdf.Tests/Drawing/RecordingCanvas.cs`
- Test: `tests/Maviray.Pdf.Tests/Drawing/PdfCanvasTests.cs`, `RecordingCanvasTests.cs`

**Interfaces:**
- Consumes: `PdfDocumentWriter`, `PdfPageWriter`, `ContentStreamBuilder`, `RgbColor`, `EmbeddedFont.Encode`, `TextLayout` and `TextLine`.
- Produces `interface ICanvas`:
  - `void BeginPage(double width, double height)` and `void EndPage()`;
  - `void PushOffset(double x, double y)` and `void PopOffset()`. Offsets add up. A `PopOffset` without a push, or `EndPage` with an offset pushed, throws `InvalidOperationException`.
  - `void FillRectangle(double x, double y, double width, double height, RgbColor color)`. Its position is the rectangle's top-left corner.
  - `void DrawTextLine(TextLayout layout, int lineIndex, double x, double y, IReadOnlyList<RgbColor> spanColors)`:
    - (x, y) is the paragraph's left edge at the line's top;
    - each run starts at x + run.X, on the baseline at y + line.Baseline;
    - each decoration's top is at y + decoration.Top.
- `NullCanvas.Instance` does nothing, and keeps no state, so the page and offset rules don't apply to it.
- `PdfCanvas(PdfDocumentWriter writer)`:
  - `BeginPage` and `EndPage` are the writer's, so each page goes out when it ends.
  - y becomes the page height minus y, and a rectangle's PDF origin is its bottom-left corner.
  - The fill colour is written only when it changes on the page.
  - Each glyph run is `BT`, `rg` when the colour changes, `Tf`, one `Tm` and `ShowGlyphs(cids, extraAdvances)`, then `ET`. Each decoration is a filled rectangle in its span's colour.
  - Each `TrueTypeFont` is embedded once per document, on first use.
- `RecordingCanvas` (test) records each call with its absolute position, as `Page`, `Rectangle` (x, y, width, height, colour) and `TextLine` (layout, index, x, y, colours) records, listed per page.

- [ ] **Step 1: Test.**
  - `PdfCanvasTests`, on `PdfDocumentWriterTests.Write` documents:
    - `ARectangleAtTheTopLeftLandsAtThePagesTopLeft`: `FillRectangle(0, 0, 100, 50, red)` on a 595 × 842 page writes `1 0 0 rg 0 792 100 50 re f`;
    - `OffsetsAddUpAndPop`: two pushes of (10, 20) and (5, 5), and a rectangle at (1, 1), land at (16, 26). After one pop, a rectangle at (0, 0) lands at (10, 20);
    - `TheFillColourIsWrittenOnceWhileItDoesntChange`;
    - `PoppingWithoutAPushThrows`, and `EndingAPageWithAnOffsetThrows`;
    - `ATextLineIsDrawnAtItsBaseline`: a line of `LayoutTestFont` text at (72, 100) writes `Tm` at x = 72 + run.X and y = height − (100 + baseline). Its `TJ` is the line's CIDs, with the extra advances of P1.7;
    - `ADecorationIsAFilledRectangleInItsSpansColour`;
    - `EachFontIsEmbeddedOnce`: two lines in one font give one font resource and one embedded font. pdffonts reports one font.
  - `RecordingCanvasTests`: `ItRecordsAbsolutePositions`.
- [ ] **Step 2:** Run them; they fail to compile.
- [ ] **Step 3:** Implement the canvases.
- [ ] **Step 4:** Run them, then the suite. Commit `#34 add the null, PDF and recording canvases`.

### Task 3: The paging loop and the passes

**Files:**
- Create: `src/Maviray.Pdf/Layout/Margins.cs`, `PageSet.cs`, `Slot.cs`, `LayoutEngine.cs`
- Create: `tests/Maviray.Pdf.Tests/Layout/FixedBlock.cs`, `SplittingBlock.cs`, `TestStack.cs`, `StallingBlock.cs`, `PageCountBlock.cs`
- Test: `tests/Maviray.Pdf.Tests/Layout/LayoutEngineTests.cs`, `ElementContract.cs`, `TestElementContractTests.cs`

**Interfaces:**
- Consumes: Task 1's protocol, and Task 2's `ICanvas` and `NullCanvas`.
- Produces:
  - `readonly record struct Margins(double Left, double Top, double Right, double Bottom)`.
  - `sealed class PageSet(double width, double height, Margins margins, RgbColor? background, Element? header, Element content, Element? footer)`. It wraps each part in a `Slot` named `Header`, `Content` or `Footer`, which measures, draws and resets its child unchanged.
  - `static class LayoutEngine` with `int Generate(IReadOnlyList<PageSet> sets, ICanvas canvas)`, which returns the page count.
- Test elements, each also passing `ElementContract`:
  - `FixedBlock(name, width, height, color)`: `Full` when it fits, else `None` with its size as `Needed`. It draws one rectangle.
  - `SplittingBlock(name, width, lineHeight, lines)`: as many lines as fit. All of them is `Full`, some is `Partial`, and none is `None`. It draws one rectangle per line, and advances past them.
  - `TestStack(name, children)`: stacks its children down from its cursor, with no spacing. It stops at the first `Partial` or `None`, is `None` only when its first remaining child is, and passes the child's source up.
  - `StallingBlock(name)`: always `Partial` with a height of 1, and never advances.
  - `PageCountBlock(name, heightForCount)`: `ShowsPageCount` is true, and its height is `heightForCount(page.PageCount)`. It records the counts it was shown, per pass.

**The page loop (L-04)**, for each set, with page numbers counted through the document:
1. `BeginPage`, and the background, filled over the whole page.
2. The inner size is the page minus the margins.
   - The header is measured at the inner size, and must be `Full`.
   - The footer is measured at the inner width and what the header leaves, and must be `Full`.
   - Either one that isn't `Full` throws: `<source path>: the <header|footer> must fit in full, because it repeats on every page. The space is <w × h>` (and `, and it needs <w × h>` when known).
3. The content is measured at the inner width, and the height between the header and the footer, never below 0. It is drawn at that size, at (left, top + header height).
   - **Full:** the set ends after this page.
   - **Partial:** if `Draw` returns false, it throws `<source path>: it split across pages but drew nothing of itself on this page. The space is <w × h>`.
   - **None:** it throws `<source path>: it can never fit. The space is <w × h>` (and `, and it needs <w × h>` when known).
4. The header is drawn at (left, top), and the footer at (left, page height − bottom − footer height), each at its measured height. Then both are reset.
   - *Amended after the review:* the header is drawn before the content, and the footer after it, so the page's stream reads in that order.
5. `EndPage`.

Each path is prefixed with `Page <n> > `.

**The passes (L-04):**
- Every slot is reset before every pass.
- **Without an element that shows the page count,** there is one pass, the render pass, on the given canvas, with `PageCount` showing the page number.
- **With one,** the layout passes run on `NullCanvas.Instance`:
  - the first shows each page's own number as the count;
  - each later one shows the count the pass before ended with;
  - they stop when a pass, from the second on, ends with the count it showed;
  - if the third pass doesn't, it throws `LayoutException("Document", "the page count keeps changing the layout: the passes ended with <a>, <b> and <c> pages.", NaN, NaN)`.
- The render pass then shows that count, with `IsRenderPass` true.

- [ ] **Step 1: Test.** `ElementContract.Check(Func<Element> make, params Size[] sizes)`, in the test project, checks L-03's contract:
  - `Measure` twice gives the same answer;
  - the size never exceeds the available size;
  - `None` has no size;
  - after `Partial`, `Draw` returns true;
  - `Reset()` brings back the first measurement.

  `TestElementContractTests` runs it on each test element except `StallingBlock`, and shows that it catches `StallingBlock`.

  `LayoutEngineTests`, on the recording canvas, with A4 pages, margins of 50 and a 10-point header and footer:
  - `ATreeOfFixedBlocksPagesCorrectly`: 30 blocks 100 high in a stack, in 722 points of content height, fill 5 pages: 7 blocks on each of the first 4 and 2 on the last, at their expected positions;
  - `TheHeaderAndFooterAreOnEveryPage`, at their positions, the header a `SplittingBlock` that draws whole each time;
  - `TheBackgroundIsFilledFirst`, on every page;
  - `ASecondPageSetStartsANewPageAndPageNumbersContinue`;
  - `AHeaderThatDoesntFitNamesIt`: `Page 1 > Header > Tall`, with the inner size;
  - `AFooterThatDoesntFitNamesIt`;
  - `ContentThatCanNeverFitNamesTheInnermostElement`: `Page 2 > Content > Stack > Big`, with its needed size;
  - `ContentThatSplitsWithoutAdvancingThrows`: `Page 1 > Content > Stack > Stalling`;
  - `WithoutAPageCountThereIsOnePass`;
  - `TheFirstLayoutPassShowsThePageNumber`, `LaterPassesShowTheCountBefore` and `TheRenderPassShowsTheFinalCount`;
  - `TheTreeIsResetBeforeEveryPass`;
  - `ACountThatKeepsChangingTheLayoutThrowsAfterThreePasses`: the height depends on the count, so that the count alternates.
- [ ] **Step 2:** Run them; they fail to compile.
- [ ] **Step 3:** Implement `Margins`, `PageSet`, `Slot` and `LayoutEngine`, and the test elements.
- [ ] **Step 4:** Run them, then the suite. Commit `#34 add the paging loop and its passes`.

### Task 4: The engine on a PDF page

**Files:**
- Test: `tests/Maviray.Pdf.Tests/Layout/LayoutPdfTests.cs`

- [ ] **Step 1: Test.** `ATreeOfFixedBlocksPassesQpdf` generates a document:
  - two page sets of the stack of Task 3, with a grey background, a header block and a footer block;
  - written on `PdfCanvas` through `PdfDocumentWriterTests.Write`, and saved with `GeneratedFiles.Save`.

  It checks:
  - `Qpdf.Check`;
  - `Poppler.Render` gives the expected number of pages;
  - on each page, the centre pixel of every block's rectangle has the block's colour at its flipped position, and a pixel in the margin has the background's.
- [ ] **Step 2:** Run it; it passes, because Tasks 2 and 3 built it. Its red step is a mutant: the PDF canvas not flipping y fails the pixel check. Restore it.
- [ ] **Step 3:** Verify the suite. Commit `#34 check the paging loop on PDF pages`.

### Task 5: Records

- [ ] Write `docs/releases/0.1.0/execution-logs/2026-11-24-issue-34-layout-engine-canvas.md`.
- [ ] Add #34's row to the release index, "In review", with the plan and the log.
- [ ] Commit `#34 add the P1.9 plan and execution log`.
- [ ] Push, and open the PR into `develop` with `Refs #34`, stacked on #43.
- [ ] Record the first CI run in the log.
