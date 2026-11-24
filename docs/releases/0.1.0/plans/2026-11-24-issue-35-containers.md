# P1.10 Containers: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The containers of L-06, each at its edges and across pages: Column, Row with fixed, flex and auto items, padding, alignment, size limits, background, borders, lines and the page break.

**Architecture:**
- **Where they live.** Each container is an `Element` (P1.9) in `src/Maviray.Pdf/Elements/`. Column items and Row items are named places in the tree, `Item n` and `Fixed n`, `Flex n` or `Auto n`, so paths read as L-05 shows.
- **How they are tested.** Each one passes `ElementContract`, and draws where the recording canvas expects, both alone and paged by `LayoutEngine`.

**Tech Stack:** .NET 10, C#, xUnit v3; qpdf and poppler for one composed page.

**Spec:** [P1 Layout and fluent API](../specs/2026-11-23-p1-layout-design.md), decision L-06, with L-03 and L-05. Work item [#35](https://github.com/Maviray/pdf/issues/35). The branch `feature/35-containers` is stacked on `feature/34-layout-engine-canvas` (PR #44).

## Global Constraints

- P1.9's constraints hold. Every type is `internal`, in `Maviray.Pdf.Elements`, with tests in `tests/Maviray.Pdf.Tests/Elements/`.
- Colours are `RgbColor`; the public `Color` of P1.12 maps onto it.
- A container's `Draw` measures its children again where it needs their sizes. It keeps no measurement between calls, because `Measure` has no side effects.
- Each task ends with one commit, subject `#35 <summary>`. The records are the last commit.

## Rulings made while planning

1. **Every child is drawn at its measured size.** L-06's "each item gets the full width" fixes the width. The height is the item's own: a Column item is drawn at the height it measured, and a Row item at the row's height.
   - **Why:** this way an `AlignBottom` item in a Column doesn't drop to the page's foot, and a vertical line in a Row fills the row.
   - **What it rests on:** measuring an element again at its measured size gives the same fit and size. `ElementContract` gains that check, and an infinite width among its sizes, because L-03 says every element handles one.
2. **Column spacing goes only between two items that both take height on the page.**
   - **Why:** a page break's `Full` part on the next page has no size, and with spacing after it, a gap would open at the top of that page.
   - **What it means:** an item with no height takes no spacing on either side, like empty text.
   - **Cost if wrong:** a zero-height item doesn't add a gap of its own.
3. **Border's box.** It is the width it is drawn at, and the child's height plus the top and bottom widths, as Background's is.
   - Each page part of a split draws all four sides, as the P1.10 tests in the spec ask: "borders and backgrounds on every page part".
   - A space of zero or less inside the borders is `None`, as for Padding.
4. **Size limits are one element.** Its name in a path is the call that made it: `Width`, `MinWidth`, `MaxWidth`, `Height`, `MinHeight` or `MaxHeight`.
   - The child gets the available width capped at the maximum, and the available height capped at the maximum.
   - It is `None` when the available width or height is below its minimum.
   - Its size is the child's, raised to the minimum. `Width(w)` and `Height(h)` set both limits.
5. **An alignment at infinite width places its child at x = 0,** because there is no right edge to align to.
6. **The error builder moves** from `LayoutEngine` into `LayoutErrors.Error(PageContext page, Element source, string reason, Size space)`, so Row can throw from `Measure`. This was a deferred point from P1.9.

## Review Focus

1. **A child measured at one size and drawn at another.** Every container draws each child at a size where the child measures the same fit and size.
2. **Row widths in each order.**
   - Fixed widths, then auto ones at infinite width, then flex shares of what is left, with spacing.
   - An item that finished on an earlier page draws nothing, and its height doesn't count.
   - Fixed and auto widths wider than the space throw, naming the Row.
3. **Paths.** `Column > Item 2 > Row > Flex 1 > ...`. An item's position counts all of the row's items, so a flexible second item is `Flex 2`.
4. **The page break.**
   - It ends the page, and its second part takes no space.
   - It counts as progress, so the engine doesn't throw.
   - No gap opens at the top of the next page.
5. **Infinite width.** Every container measures at an infinite width without returning an infinite size, except where its children ask for one. Row measures its flex items there as auto ones.

---

### Task 1: Shared pieces

**Files:**
- Modify: `src/Maviray.Pdf/Layout/LayoutErrors.cs`, `LayoutEngine.cs`, `Slot.cs` (its summary: any named place, a page slot, a Column item or a Row item)
- Modify: `tests/Maviray.Pdf.Tests/Layout/ElementContract.cs`
- Create: `tests/Maviray.Pdf.Tests/Elements/ElementCanvas.cs`
- Test: `tests/Maviray.Pdf.Tests/Layout/TestElementContractTests.cs`, `LayoutExceptionTests.cs`

**Interfaces:**
- Produces:
  - `LayoutErrors.Error(PageContext page, Element source, string reason, Size space)`, which returns the `LayoutException` with `Page n > ` first;
  - `ElementContract.Check(...)`, which also measures at each measured size and requires the same fit and size, and always adds an infinite-width size;
  - `ElementCanvas.Draw(Element element, Size space)`, which measures and draws once on a fresh `RecordingCanvas` page of 595 × 842 and returns the calls.
- [ ] **Step 1: Test.**
  - `LayoutExceptionTests.TheErrorBuilderPutsThePageFirst`.
  - `TestElementContractTests.TheCheckCatchesASizeThatMeasuresDifferently`: a test element that is `Full` at its size + 1, but `None` at its size.
- [ ] **Step 2:** Run red. Implement. Run the suite. Commit `#35 share the error builder and check measured sizes`.

### Task 2: Column

**Files:**
- Create: `src/Maviray.Pdf/Elements/Column.cs`
- Test: `tests/Maviray.Pdf.Tests/Elements/ColumnTests.cs`

**Interfaces:**
- Produces `Column(IEnumerable<Element> items, double spacing)`, named `Column`. Each item is wrapped in a `Slot` named `Item n`, from 1.

**Behaviour (L-06, ruling 2):**
- It measures items in order from its cursor, at the full width and the height left. It stops at the first item that is `Partial` or `None`.
  - It is `None` only when its first remaining item is, and passes that item's measurement up.
  - A `Partial` item's split is passed up with `WithSize`.
  - An item that is `None` after others fit makes the Column `Partial` with itself as the source.
- Its width is the widest item's, and its height is the sum, with spacing between items that both take height on the page.
- It draws each item at (the width, the item's measured height), moves past finished items, and advanced if any child did or its cursor moved.
- **Validation:** `spacing` zero or more and finite.

- [ ] **Step 1: Test.** `ColumnTests`:
  - `ItemsStackWithSpacing`: three blocks of 50, 30 and 20 with spacing 10 draw at y 0, 60 and 100, and measure 120;
  - `ItsWidthIsTheWidestItems`;
  - `ItSplitsAtTheFirstItemThatDoesntFit`, and `SpacingDoesntCarryToTheNextPage`: through `LayoutEngine`, page 2 starts at the content's top;
  - `ASplittingItemContinuesOnTheNextPage`;
  - `ItIsNoneOnlyWhenItsFirstRemainingItemIs`, with the item's path `... > Column > Item 1 > Big`;
  - `AnItemWithNoHeightTakesNoSpacing`;
  - `FinishedItemsNeverDrawAgain`;
  - `KeepsTheContract`, with fixed, splitting and nested Columns;
  - `RefusesNegativeSpacing`.
- [ ] **Step 2:** Run red. Implement. Run the suite. Commit `#35 add Column`.

### Task 3: Row

**Files:**
- Create: `src/Maviray.Pdf/Elements/Row.cs`, `RowItemKind.cs`, `RowItem.cs`
- Test: `tests/Maviray.Pdf.Tests/Elements/RowTests.cs`

**Interfaces:**
- Produces:
  - `enum RowItemKind { Fixed, Flex, Auto }`;
  - `readonly record struct RowItem(RowItemKind Kind, double Value, Element Element)`, where `Value` is the width for `Fixed`, the weight for `Flex`, and unused for `Auto`;
  - `Row(IEnumerable<RowItem> items, double spacing)`, named `Row`. Each item is wrapped in a `Slot` named `<Kind> <position>`, its position among all the row's items, from 1.

**Behaviour (L-06):**
- **Widths:**
  - fixed widths first;
  - then each auto item's width, measured at an infinite width and the available height;
  - then the flex items share what is left after the fixed, auto and spacing widths, by weight.
  - At an infinite width, flex items are measured like auto items.
  - If the fixed and auto widths and the spacing exceed the width, `Measure` throws `LayoutErrors.Error(page, row, "its fixed and auto items and spacing need <n> pt, and the space is <w> pt.", available)`.
- **Fit:** each unfinished item is measured at (its width, the available height).
  - The Row is `None` if any item is, passing the first such item's measurement up.
  - It is `Partial` if any item is, with the first partial item's source.
  - Otherwise it is `Full`.
- **Size:** its width is the sum of its item widths and spacing. Its height is the tallest unfinished item's.
- **Draw:** every unfinished item draws at (its width, the row's height), at its x. An item that finished on an earlier page draws nothing.
- **Validation:** fixed widths zero or more and finite, flex weights positive and finite, spacing zero or more.

- [ ] **Step 1: Test.** `RowTests`:
  - `FixedThenAutoThenFlex`: in 495 points with spacing 5, a fixed 100, an auto child of 60 and two flex items of weight 1 and 3 share the 320 points left: they get x 0, 105, 170 and 255, and widths 100, 60, 80 and 240;
  - `ItemsAreNamedByKindAndPosition`: a flexible second item's child reports `Row > Flex 2 > Child`;
  - `ItsHeightIsTheTallestItems`, and every item is drawn at that height. A vertical line fills it.
  - `AnItemFinishedEarlierDrawsNothing`, through `LayoutEngine`: a short and a long splitting item;
  - `ItIsNoneIfAnyItemIs`, and `PartialIfAnyItemIs`;
  - `FixedAndAutoWiderThanTheSpaceThrows`, with the Row's path and the message;
  - `AtInfiniteWidthFlexItemsMeasureLikeAuto`;
  - `KeepsTheContract`;
  - `RefusesBadWidthsAndWeights`.
- [ ] **Step 2:** Run red. Implement. Run the suite. Commit `#35 add Row`.

### Task 4: Padding, alignment and size limits

**Files:**
- Create: `src/Maviray.Pdf/Elements/Padding.cs`, `Align.cs`, `HorizontalAlignment.cs`, `VerticalAlignment.cs`, `SizeLimit.cs`
- Test: `tests/Maviray.Pdf.Tests/Elements/PaddingTests.cs`, `AlignTests.cs`, `SizeLimitTests.cs`

**Interfaces:**
- Produces:
  - `Padding(double left, double top, double right, double bottom, Element child)`, named `Padding`;
  - `enum HorizontalAlignment { Left, Center, Right }`, and `enum VerticalAlignment { Top, Middle, Bottom }`;
  - `Align(HorizontalAlignment? horizontal, VerticalAlignment? vertical, Element child)`, named `Align`;
  - `SizeLimit(string name, double minWidth, double maxWidth, double minHeight, double maxHeight, Element child)`, with 0 and infinity for no limit, and the factories `Width(w, child)`, `MinWidth`, `MaxWidth`, `Height`, `MinHeight` and `MaxHeight`.

**Behaviour (L-06, rulings 4 and 5):**
- **Padding:**
  - the child gets the space minus the padding, and a space of zero or less is `None`, with the Padding as its source;
  - the size is the child's plus the padding;
  - the child is drawn offset by the left and top padding.
- **Align:**
  - its measurement is the child's, measured at the space;
  - it draws the child at its measured size, placed left, centre or right in the width it is drawn at, and top, middle or bottom in the height. On a `Partial`, it stays at the top.
  - An axis that is null stays at the start.
- **SizeLimit:**
  - the child gets the available width capped at the maximum width, and the same for the height;
  - it is `None` with itself as the source when the available width or height is below the minimum;
  - its size is the child's, raised to the minimums.
- **Validation:** padding and limits zero or more and finite, maximums may be infinite, and a minimum above its maximum throws.

- [ ] **Step 1: Test.**
  - `PaddingTests`:
    - `TheChildIsInsetAndTheSizeGrows`;
    - `ASpaceOfZeroOrLessIsNone`;
    - `ItSplitsWithItsChild`, through `LayoutEngine`, with the padding on every page part;
    - `KeepsTheContract`.
  - `AlignTests`:
    - each of the nine placements in a space of 200 × 100 around a child of 50 × 20;
    - `APartialChildStaysAtTheTop`;
    - `AtInfiniteWidthItPlacesTheChildAtZero`;
    - `InAColumnItAlignsWithinTheItem`: an `AlignBottom` item draws at its own top;
    - `KeepsTheContract`.
  - `SizeLimitTests`:
    - `WidthGivesExactlyW` when the child is narrower and when wider, and `None` when the space is narrower;
    - `MaxWidthCapsTheChild`, and `MinWidthRaisesTheSize`;
    - `MaxHeightGivesAtMostHOnEachPage`, through `LayoutEngine`;
    - `MinHeightRaisesEachPartAndIsNoneWithLessLeft`;
    - `HeightIsBoth`;
    - `AtInfiniteWidthWidthGivesW`;
    - `KeepsTheContract`;
    - `RefusesAMinimumAboveItsMaximum`.
- [ ] **Step 2:** Run red. Implement. Run the suite. Commit `#35 add padding, alignment and size limits`.

### Task 5: Background, border, lines and the page break

**Files:**
- Create: `src/Maviray.Pdf/Elements/Background.cs`, `Border.cs`, `HorizontalLine.cs`, `VerticalLine.cs`, `PageBreak.cs`
- Test: `tests/Maviray.Pdf.Tests/Elements/BackgroundTests.cs`, `BorderTests.cs`, `LineTests.cs`, `PageBreakTests.cs`

**Interfaces:**
- Produces:
  - `Background(RgbColor color, Element child)`, named `Background`;
  - `Border(double left, double top, double right, double bottom, RgbColor color, Element child)`, named `Border`;
  - `HorizontalLine(double thickness, RgbColor color)` and `VerticalLine(double thickness, RgbColor color)`, named `HorizontalLine` and `VerticalLine`;
  - `PageBreak()`, named `PageBreak`.

**Behaviour (L-06, ruling 3):**
- **Background** measures as its child. It fills (0, 0, the width it is drawn at, the child's height), then draws the child, on every page part.
- **Border:**
  - the child gets the space minus the border widths, and a space of zero or less is `None`;
  - its size is the child's plus the widths;
  - it draws the child, then fills the four bands of its box, on every page part.
- **HorizontalLine:** `Full` with width 0 and height the thickness, or `None` when the height is less. It fills the width it is drawn at.
- **VerticalLine:** `Full` with width the thickness and height 0, or `None` when the width is less. It fills the height it is drawn at.
- **PageBreak:**
  - the first time, it is `Partial` with no size; after that, `Full` with no size;
  - its draw marks it passed and advances;
  - `Reset` clears the mark.
- **Validation:** thicknesses and border widths zero or more and finite.

- [ ] **Step 1: Test.**
  - `BackgroundTests`: `ItFillsTheWidthGivenAndTheChildsHeightFirst`, and `OnEveryPagePart`, through `LayoutEngine`, with a splitting child; `KeepsTheContract`.
  - `BorderTests`:
    - `TheBorderTakesSpace`;
    - `ItDrawsTheChildThenFourBands`, at their positions;
    - `OnEveryPagePart`;
    - `ASpaceOfZeroOrLessIsNone`;
    - `KeepsTheContract`.
  - `LineTests`:
    - `AHorizontalLineFillsTheWidthGiven`, and `AVerticalLineFillsTheRowsHeight`;
    - `ALineThatDoesntFitIsNone`;
    - `AtInfiniteWidthAHorizontalLineIsZeroWide`;
    - `KeepsTheContract`.
  - `PageBreakTests`, through `LayoutEngine` with a Column of `A, PageBreak, B` and spacing 10:
    - `TheContentAfterItStartsANewPage`, with B at the content's top on page 2, so no spacing;
    - `ItCountsAsProgress`;
    - `TwoBreaksInARowGiveOneEmptyPage`;
    - `ResetBringsItBack`.
- [ ] **Step 2:** Run red. Implement. Run the suite. Commit `#35 add background, border, lines and the page break`.

### Task 6: A composed page in PDF

**Files:**
- Test: `tests/Maviray.Pdf.Tests/Elements/ContainersPdfTests.cs`

- [ ] **Step 1: Test.** `AComposedTreePassesQpdf`: an invoice-like tree of fixed blocks over three pages:
  - a Column of Rows with fixed, auto and flex items;
  - padding, a background, a border and lines;
  - an aligned total, and a page break.

  It is generated on the recording canvas and on `PdfCanvas`. qpdf accepts it, and every rectangle's centre renders in its colour where the recording canvas saw it, as in P1.9's PDF test.
- [ ] **Step 2:** Its red step is a mutant: Row placing flex items before auto ones fails the positions. Restore it. Commit `#35 check the containers on PDF pages`.

### Task 7: Records

- [ ] Write `docs/releases/0.1.0/execution-logs/2026-11-24-issue-35-containers.md`.
- [ ] Add #35's row to the release index, "In review".
- [ ] Commit `#35 add the P1.10 plan and execution log`.
- [ ] Push, and open the PR into `develop` with `Refs #35`, stacked on #44.
- [ ] Record the first CI run in the log.
