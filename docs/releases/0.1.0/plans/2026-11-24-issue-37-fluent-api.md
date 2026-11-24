# P1.12 Fluent API, settings and metadata: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The public API of 0.1.0 (L-02), its settings and saving (L-11), metadata (L-12), deterministic mode (L-13), the public API listing (L-16), and the README example (L-18). A document written with the public API passes qpdf, and the README example runs.

**Architecture:**
- **Two stages.** Builders record what the compose callback asks for as a tree of internal nodes. The elements of P1.9 to P1.11 are built from that tree only after the callback returns. Building last means:
  - a Text element is made only after its span calls finish (P1.11 review);
  - the merged default text style passes down (P1.11 ruling 2);
  - chained alignments merge into one Align (P1.10 review).
- **Generation.** A `Document` runs the compose callback again on every save. It builds the page sets and generates them with `LayoutEngine` on a `PdfCanvas` over a `PdfDocumentWriter`.
- **The writer** gains the Info dictionary, `/Lang`, `/ViewerPreferences`, and the hashed `/ID` of deterministic mode.

**Tech Stack:** .NET 10, C#, xUnit v3; qpdf and poppler.

**Spec:** [P1 Layout and fluent API](../specs/2026-11-23-p1-layout-design.md), decisions L-01, L-02, L-11, L-12, L-13, L-16 and L-18, with L-06 as amended (stretching backgrounds and borders). Work item [#37](https://github.com/Maviray/pdf/issues/37). The branch `feature/37-fluent-api` is stacked on `feature/36-text-element-styles-fonts` (PR #46).

## Global Constraints

- Only the fluent API is public, in `src/Maviray.Pdf/Fluent/`, namespace `Maviray.Pdf`. The nodes and everything below them stay `internal`.
- Every public type and member has XML docs. Each argument is checked at the call, and throws `ArgumentException` or `ArgumentOutOfRangeException` naming the parameter (L-02).
- **Builders are single-use** (L-02):
  - a container takes one child, and a second throws `InvalidOperationException`;
  - `IContainer` has no members, and a foreign implementation throws `ArgumentException`.
- No public name copies one distinctive to QuestPDF (L-02's *own* rows).
- No reflection in `src/`. The package version for `/Producer` comes from a constant the build writes.
- Each task ends with one commit, subject `#37 <summary>`. The records are the last commit.

## Rulings made while planning

1. **Page defaults** (L-02 doesn't fix them): A4, no margins, no background. A page set without `Content()` has an empty Column as its content.
2. **The text call gets no element-level style.**
   - A Text element's own style is the empty style. Its spans' styles sit over the inherited default.
   - **Why:** L-02's text descriptor has spans and alignment only. The P1.11 review asked P1.12 to decide.
3. **`DefaultTextStyle(Func<TextStyle, TextStyle>)` receives an empty style.** What it returns sits over the inherited default, on a page and on any container.
4. **Chained alignments merge.** An `Align` node whose child is an `Align` node builds one Align, with the inner call winning on an axis both set. So `.AlignRight().AlignBottom()` places the child at the bottom right.
5. **`Deterministic()` turns it on.** `Deterministic(false)` turns it off again.
6. **The API listing lives at `src/Maviray.Pdf/PublicApi.txt`.**
   - The test lists the exported types in ordinal order, each with its public members' signatures, using reflection in the test project only.
   - `MAVIRAY_PDF_WRITE_PUBLIC_API=true` rewrites the file.
   - P1.13 adds the listing of `Maviray.Pdf.Fonts.Default`.

## Review Focus

1. **A builder used twice:** a second child, a slot asked for twice, or a `TextSpanBuilder` styled after its element was built.
2. **Saving:**
   - `Save(Stream)` never disposes the caller's stream;
   - `Save(path)` leaves an existing file unchanged and no temporary file behind when generation throws;
   - every save runs the callback again.
3. **Deterministic mode.**
   - Two saves give identical bytes.
   - `/ID` is the first 16 bytes of the SHA-256 of everything before the trailer.
   - No `CreationDate` unless one is set.
4. **Lengths in every unit.** Each overload with a `Unit` converts exactly: 72 points to the inch, 25.4 mm to the inch.
5. **Metadata as text strings:** a title in Turkish and Cyrillic reads back exactly, and `/Lang` and `DisplayDocTitle` are written only when set.

---

### Task 1: The writer's metadata and hashed ID

**Files:**
- Modify: `src/Maviray.Pdf/Writer/PdfWriterOptions.cs`, `PdfFileWriter.cs`, `PdfOutput.cs`, `PdfDocumentWriter.cs`
- Create: `src/Maviray.Pdf/Writer/PdfDocumentInfo.cs`, `src/Maviray.Pdf/Writer/PdfDate.cs`
- Modify: `src/Maviray.Pdf/Maviray.Pdf.csproj`, for a build target that writes `PackageVersion.g.cs` with `internal static class PackageVersion { public const string Value = "$(Version)"; }` into the intermediate folder
- Test: `tests/Maviray.Pdf.Tests/Writer/MetadataTests.cs`

**Interfaces:**
- `internal sealed record PdfDocumentInfo(string? Title, string? Author, string? Subject, string? Keywords, string? Creator, DateTimeOffset? CreationDate)`.
- `PdfWriterOptions` gains:
  - `PdfDocumentInfo? Info`;
  - `string? Language`;
  - `bool HashedFileId`: the `/ID` is the first 16 bytes of the SHA-256 of every byte before the trailer, and it can't be combined with `FileId`.
- `PdfDate.Format(DateTimeOffset)`, which gives `D:YYYYMMDDHHmmSS+HH'mm'` (`Z` when the offset is 0).

**Behaviour (L-12, L-13):**
- The Info dictionary holds each value that is set, as a text string, and always `/Producer (Maviray.Pdf <version>)`. The trailer points at it.
- The catalog gets `/Lang` when a language is set, and `/ViewerPreferences <</DisplayDocTitle true>>` when a title is set.

- [ ] **Test:**
  - `TheInfoDictionaryHoldsWhatIsSet`, with a Turkish and Cyrillic title read back by `pdfinfo`;
  - `ProducerIsAlwaysWritten`;
  - `UnsetValuesArentWritten`;
  - `LangAndDisplayDocTitleOnlyWhenSet`;
  - `TheDateFormat` (`+01'00'`, `-05'30'`, `Z`);
  - `AHashedIdIsTheSha256OfTheFileBeforeTheTrailer`, recomputed in the test;
  - `AHashedIdAndAGivenIdCantBeCombined`;
  - `qpdf --check` on the metadata file.
- [ ] Red, implement, the suite. Commit `#37 add the writer's metadata and hashed file ID`.

### Task 2: Units, page sizes and the container nodes

**Files:**
- Create in `src/Maviray.Pdf/Fluent/`: `Unit.cs`, `PageSize.cs`, `IContainer.cs`, `ContainerExtensions.cs`, `ColumnBuilder.cs`, `RowBuilder.cs`
- Create in `src/Maviray.Pdf/Fluent/Nodes/` (namespace `Maviray.Pdf.Fluent.Nodes`, internal): `Node.cs`, `ContainerSlot.cs`, and one node per container
- Test: `tests/Maviray.Pdf.Tests/Fluent/UnitTests.cs`, `PageSizeTests.cs`, `ContainerExtensionsTests.cs`

**Interfaces:**
- `public enum Unit { Point, Millimetre, Centimetre, Inch }`, with an internal `ToPoints(double, Unit)`.
- `public readonly struct PageSize`:
  - `Width` and `Height` in points;
  - `A3`, `A4`, `A5`, `Letter` and `Legal`, in portrait;
  - `Landscape()`, which swaps the sides so the width is the longer one.
- `public interface IContainer`, with no members.
- `ContainerExtensions`, extension methods on `IContainer`, each with a `Unit` overload for every length:
  - `Column(Action<ColumnBuilder>)` and `Row(Action<RowBuilder>)`;
  - `Padding(all)`, `PaddingHorizontal`, `PaddingVertical`, `PaddingLeft`, `PaddingTop`, `PaddingRight` and `PaddingBottom`;
  - `AlignLeft`, `AlignCenter`, `AlignRight`, `AlignTop`, `AlignMiddle` and `AlignBottom`;
  - `Width`, `MinWidth`, `MaxWidth`, `Height`, `MinHeight` and `MaxHeight`;
  - `Background(Color, bool stretch = true)`;
  - `Border(width, Color, bool stretch = true)`, and `BorderLeft`, `BorderTop`, `BorderRight` and `BorderBottom` the same way;
  - `HorizontalLine(thickness, Color)`, `VerticalLine(thickness, Color)` and `PageBreak()`;
  - `DefaultTextStyle(Func<TextStyle, TextStyle>)`;
  - `Text(string)` and `Text(Action<TextBuilder>)`, which come in Task 3.
  - Each wrapping call returns the `IContainer` of its child.
- `ColumnBuilder`: `Spacing(double)` with a `Unit` overload, and `IContainer Item()`.
- `RowBuilder`: `Spacing`, `IContainer Fixed(double width)` with a `Unit` overload, `IContainer Flex(double weight = 1)`, and `IContainer Auto()`.
- Internal:
  - `Node`, with `Element Build(BuildContext context)`;
  - `ContainerSlot : IContainer`, which takes one node and throws on a second;
  - `BuildContext(TextStyle Inherited, FontRegistry Fonts)`.

- [ ] **Test:**
  - `UnitTests`: `ToPoints` for every unit (25.4 mm is 72 pt);
  - `PageSizeTests`:
    - A4 is 595.2756 × 841.8898;
    - `Landscape` swaps the sides;
    - Letter is 612 × 792;
  - `ContainerExtensionsTests`, each building its element tree and checking it with the recording canvas:
    - every call makes its element;
    - a second child throws `InvalidOperationException`;
    - a foreign `IContainer` throws `ArgumentException`;
    - negative and non-finite lengths throw, naming the parameter;
    - a `Unit` overload equals the point one;
    - `.AlignRight().AlignBottom()` places the child at the bottom right;
    - `DefaultTextStyle` reaches the text inside.
- [ ] Red, implement, the suite. Commit `#37 add units, page sizes and the container calls`.

### Task 3: Text builders

**Files:**
- Create in `src/Maviray.Pdf/Fluent/`: `TextBuilder.cs`, `TextSpanBuilder.cs`
- Create: `src/Maviray.Pdf/Fluent/Nodes/TextNode.cs`
- Test: `tests/Maviray.Pdf.Tests/Fluent/TextBuilderTests.cs`

**Interfaces:**
- `TextSpanBuilder`, with `FontFamily`, `FontSize`, `Weight`, `Bold`, `Italic`, `FontColor`, `Underline`, `Strikethrough`, `LetterSpacing` and `LineHeight`. Each sets the span's style and returns the builder, with `TextStyle`'s checks.
- `TextBuilder`:
  - `TextSpanBuilder Span(string text)`;
  - `TextSpanBuilder PageNumber()` and `TextSpanBuilder PageCount()`;
  - `AlignLeft()`, `AlignCenter()` and `AlignRight()`.
- `IContainer.Text(string)` returns the span's `TextSpanBuilder`, and `IContainer.Text(Action<TextBuilder>)` returns nothing.

- [ ] **Test:**
  - `ASpanIsStyledByItsBuilder`, whose bold span draws in Noto Sans Bold;
  - `PageNumberAndPageCountSpans`;
  - `AlignmentApplies`;
  - `AStyleCallAfterBuildingThrows`: styling a `TextSpanBuilder` after the document is built throws `InvalidOperationException`;
  - `ANullTextThrows`.
- [ ] Red, implement, the suite. Commit `#37 add the text builders`.

### Task 4: Documents, pages, settings and metadata

**Files:**
- Create in `src/Maviray.Pdf/Fluent/`: `Document.cs`, `DocumentBuilder.cs`, `PageBuilder.cs`, `SettingsBuilder.cs`, `MetadataBuilder.cs`
- Test: `tests/Maviray.Pdf.Tests/Fluent/DocumentTests.cs`, `PageBuilderTests.cs`, `SettingsTests.cs`, `MetadataBuilderTests.cs`

**Interfaces (L-02, L-11, L-12):**
- `Document.Create(Action<DocumentBuilder> compose)`.
- `Save(Stream)`, `Save(string path)` and `byte[] ToBytes()`.
- `DocumentBuilder`: `Settings(Action<SettingsBuilder>)`, `Metadata(Action<MetadataBuilder>)` and `Page(Action<PageBuilder>)`.
- `PageBuilder`:
  - `Size(PageSize)`, and `Size(width, height, Unit unit = Unit.Point)`;
  - `Margin`, `MarginHorizontal`, `MarginVertical`, `MarginLeft`, `MarginTop`, `MarginRight` and `MarginBottom`, with `Unit` overloads;
  - `Background(Color)` and `DefaultTextStyle(Func<TextStyle, TextStyle>)`;
  - `Header()`, `Content()` and `Footer()`, each callable once.
- `SettingsBuilder`: `Deterministic(bool value = true)`, `Compression(CompressionLevel)`, `Fonts(FontRegistry)` and `OnDiagnostic(Action<Diagnostic>)`. The defaults are off, `Optimal`, `FontRegistry.Default` and none.
- `MetadataBuilder`:
  - `Title`, `Author`, `Subject`, `Keywords` and `Creator`;
  - `Language(string)`, checked for BCP 47 syntax: letters and digits in hyphen-separated parts of 1 to 8 characters;
  - `CreationDate(DateTimeOffset)`.

**Behaviour (L-11, L-13):**
- Each save runs the callback again and generates the document.
- `Save(Stream)` doesn't dispose the stream.
- `Save(path)` writes `<path>.<guid>.tmp` beside the target, then moves it into place, replacing the target. A failure deletes the temporary file and leaves the target unchanged.
- A document with no page sets throws `InvalidOperationException`.
- Without deterministic mode, `CreationDate` defaults to the time the save starts. In deterministic mode it is written only when set, and the `/ID` is hashed.

- [ ] **Test:**
  - `SaveToAStreamLeavesItOpen`;
  - `SaveToAPathMovesTheFileIntoPlace`;
  - `AFailedSaveLeavesTheTargetAndNoTemporaryFile`: an existing file and an element that can never fit;
  - `ToBytesEqualsTheStream`;
  - `EverySaveRunsTheCallbackAgain`;
  - `NoPageSetsThrows`;
  - `DeterministicSavesAreIdentical`, and `TheyDifferWithoutIt`, by `/ID`;
  - `TheCreationDateDefaultsToNowUnlessDeterministic`;
  - `DiagnosticsReachTheHandler`;
  - `ACustomRegistryIsUsed`;
  - `PageSizesMarginsAndBackgroundApply`;
  - `ASlotAskedForTwiceThrows`;
  - `LanguageTagsAreChecked` (`de-DE` and `sr-Latn-RS` pass; `de_DE`, `toolongpart9` and an empty one fail);
  - `AWholeDocumentPassesQpdf`: a two-page document with a header, a footer with `Seite n von N`, Rows, a Column and text, checked by qpdf, pdffonts and pdftotext.
- [ ] Red, implement, the suite. Commit `#37 add documents, pages, settings and metadata`.

### Task 5: The public API listing

**Files:**
- Create: `tests/Maviray.Pdf.Tests/Fluent/PublicApiTests.cs`, `src/Maviray.Pdf/PublicApi.txt`

- [ ] **Test:** `ThePublicApiMatchesItsListing`:
  - each exported type, by its full name, with its kind and base type;
  - then each public or protected member, sorted ordinally, as its signature: return type, name, generic arity, parameters with their types and default values, and `static`;
  - the whole list is compared with the file, and the first differing line is shown on a mismatch.
- [ ] Write the file with the variable set, read it through, and check that it holds L-02's names and nothing below the fluent layer. Commit `#37 add the public API listing`.

### Task 6: The README example

**Files:**
- Modify: `README.md` ("Getting started")
- Create: `tests/Maviray.Pdf.Tests/Fluent/ReadmeExampleTests.cs`

- [ ] **Test:** `TheReadmeExampleRuns`. The test holds the example's code between `// README begin` and `// README end` markers. It checks that:
  - those lines equal the README's first `csharp` block under "Getting started", character for character, after removing the test's indentation;
  - the code makes a document that passes qpdf.
- [ ] Write the example: a one-page invoice-like document in Noto Sans, using a registry with the four files, since `RegisterNotoSans()` comes in P1.13. Commit `#37 add the README example and its test`.

### Task 7: Records

- [ ] The log, the index row, `#37 add the P1.12 plan and execution log`, the PR stacked on #46, and the CI run.
