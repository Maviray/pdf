# P1.11 Text element, styles and fonts: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The Text element of L-07, the text styles of L-08, the font registry of L-09 and the diagnostics of L-14. A multi-page paragraph splits by line, and every page passes qpdf.

**Architecture:**
- **The Text element** is an `Element` (P1.9) in `src/Maviray.Pdf/Elements/`. It resolves its spans' styles and fonts, prepares one paragraph with the text engine (T-14), and fills it at the available width, keeping the last fill. It measures and draws whole lines from a cursor, and re-prepares when a page-number span's number changes.
- **The public types**, named by L-02, are the first besides `LayoutException`: `Color`, `FontWeight`, `TextStyle`, `FontRegistry`, `Diagnostic` and `DiagnosticKind`, in `src/Maviray.Pdf/Fluent/`, namespace `Maviray.Pdf`.
- **Diagnostics** reach a sink through `PageContext`. The sink keeps each one to once per document.

**Tech Stack:** .NET 10, C#, xUnit v3; qpdf and poppler for the multi-page paragraph.

**Spec:** [P1 Layout and fluent API](../specs/2026-11-23-p1-layout-design.md), decisions L-07, L-08, L-09 and L-14, with L-02 for the public names. Work item [#36](https://github.com/Maviray/pdf/issues/36). The branch `feature/36-text-element-styles-fonts` is stacked on `feature/35-containers`.

## Global Constraints

- P1.9's and P1.10's constraints hold.
- Every public type and member has XML docs, in the spec's words, and every argument is checked at the call (L-02).
- `FontRegistry` is safe to use from many threads (NFR-04). `FontRegistry.Default` is process-wide.
- No public type below the fluent layer. The element, the resolved style and the sink stay `internal`.
- Each task ends with one commit, subject `#36 <summary>`. The records are the last commit.

## Rulings made while planning

1. **The engine's `TextStyle` becomes `SpanStyle`.**
   - L-02 makes `TextStyle` public in `Maviray.Pdf`. Every test namespace lies inside `Maviray.Pdf`, so C# would find the public type before the engine's `Maviray.Pdf.Text.TextStyle`, and every text test would stop compiling.
   - The rename is internal, and T-14 names no type.
   - **Cost if wrong:** none; it is mechanical.
2. **Container defaults are merged by the fluent layer, not by elements.**
   - A `DefaultTextStyle` on a container affects the elements built inside it. P1.12's builders run outside in, so each one passes the merged default down.
   - The Text element therefore takes two styles besides its spans' own: its own style, and the inherited one, which is the nearest container's default over each outer one, over the page's.
   - Paths gain no `DefaultTextStyle` element.
   - **Cost if wrong:** P1.12 must merge as it builds, which is how its builders work anyway.
3. **`Italic`, `Underline` and `Strikethrough` take `bool value = true`.** Without it, a span couldn't undo an inherited underline. The names stay L-02's.
4. **The Text element's cursor is a line index.** *Replaced after the review:* its premise was wrong. An Align, a Row's Auto item and a widening Flex item draw a Text at another width than it was measured at, and the round trip holds only at the whole fill's widest line. The cursor is now a position in the text, and each page part fills the rest at its own width (T-14, amended).
   - A paragraph that continues on another page keeps the width it was filled at, because a page set's pages are the same size. Drawing it at its measured width gives the same lines (T-09's round trip).
   - **Cost if wrong:** a paragraph whose page-number span changes between its own pages is filled again, and continues at the same line index.
5. **Diagnostics name the element the way errors do,** with `Page n > ` first, and also carry the page number. They are raised in the render pass, when the element first draws.

## Review Focus

1. **The cached fill.**
   - A second measure at the same width doesn't fill again, and neither does a switch between infinity and a width.
   - A new width fills again.
   - A page-number span's new number prepares again, and so fills again.
2. **Splitting by line.**
   - A paragraph that ends exactly at a page's foot is `Full` there.
   - A page that fits no line gives `None`, with the first line's height as what it needs.
   - Each page part draws its first line at its own top.
3. **Weight matching by CSS Fonts 4.** For each rule the spec lists: 400 tries 500 first, 500 tries 400 first, below 400 tries lighter first, above 500 tries heavier first. Italic falls back to upright and the reverse, each raising one diagnostic.
4. **A family that isn't registered**, and the default `Noto Sans` in particular, whose message names `Maviray.Pdf.Fonts.Default` and `RegisterNotoSans()`.
5. **Once per document.** A code point missing from one font, or one substituted face, is reported once, however many elements and pages meet it, and only in the render pass.

---

### Task 1: The engine's style renamed

**Files:** `src/Maviray.Pdf/Text/TextStyle.cs` → `SpanStyle.cs`, `tests/Maviray.Pdf.Tests/Text/TextStyleTests.cs` → `SpanStyleTests.cs`, and every use (14 files).

- [ ] Rename with a word-bounded replace, run the suite, and commit `#36 rename the engine's span style to SpanStyle`. A rename changes no behaviour: the suite is its test.

### Task 2: Colour and weight

**Files:**
- Create: `src/Maviray.Pdf/Fluent/Color.cs`, `FontWeight.cs`
- Test: `tests/Maviray.Pdf.Tests/Fluent/ColorTests.cs`

**Interfaces:**
- `public readonly struct Color : IEquatable<Color>`:
  - `R`, `G` and `B` as bytes;
  - `FromRgb(byte, byte, byte)`, `FromHex(string)`, `Black` and `White`;
  - an internal `RgbColor ToRgb()`.
- `FromHex` takes `#RRGGBB` or `#RGB`, with the `#`, in either case. Anything else throws `ArgumentException` naming `hex`.
- `public enum FontWeight`: `Thin = 100`, `ExtraLight = 200`, `Light = 300`, `Normal = 400`, `Medium = 500`, `SemiBold = 600`, `Bold = 700`, `ExtraBold = 800`, `Black = 900`.

- [ ] **Test:**
  - `FromHexReadsBothForms` (`#1F4E79`, `#1f4e79` and `#ABC` as `#AABBCC`);
  - `FromHexRefusesMalformedText` (`1F4E79`, `#12345`, `#GGGGGG`, empty and null);
  - `TheComponentsMapOntoRgbColor`;
  - `BlackAndWhite`.
- [ ] Red, implement, the suite. Commit `#36 add Color and FontWeight`.

### Task 3: The font registry

**Files:**
- Create: `src/Maviray.Pdf/Fluent/FontRegistry.cs`, `src/Maviray.Pdf/Fonts/FontFace.cs`, `src/Maviray.Pdf/Fonts/FontMatch.cs`, `src/Maviray.Pdf/Fonts/WeightMatching.cs`
- Test: `tests/Maviray.Pdf.Tests/Fluent/FontRegistryTests.cs`, `tests/Maviray.Pdf.Tests/Fonts/WeightMatchingTests.cs`

**Interfaces:**
- `public sealed class FontRegistry`:
  - `static FontRegistry Default` and `FontRegistry()`;
  - `Register(string family, byte[] data, FontWeight? weight = null, bool? italic = null)`, with overloads taking `Stream` (read to its end, not disposed) and `string path`. Each returns the registry.
  - The font loads at once, so `NotSupportedException` and `InvalidDataException` reach the caller.
  - A face's weight and style come from the font (`WeightClass`, `IsItalic`) unless given.
  - Families compare by ordinal, ignoring case. A second face with the same family, weight and style throws `ArgumentException`.
  - A blank family and a weight outside 1 to 1,000 throw. A lock keeps it safe across threads.
  - Internal:
    - `FontMatch? Match(string family, int weight, bool italic)`, null when the family isn't registered;
    - `IReadOnlyList<string> Families`, in registration order and spelled as registered.
- `internal sealed record FontFace(string Family, int Weight, bool Italic, TrueTypeFont Font)`.
- `internal sealed record FontMatch(FontFace Face, bool Substituted)`.
- `internal static class WeightMatching` with `int Choose(int desired, IReadOnlyCollection<int> available)`, by CSS Fonts 4:
  - from 400 to 500, the weights from the desired one up to 500, ascending, then lighter ones descending, then heavier than 500 ascending;
  - below 400, lighter or equal descending, then heavier ascending;
  - above 500, heavier or equal ascending, then lighter descending.
- **Matching (L-09):**
  - italic requests use the italic faces if any, else the upright ones, and the reverse;
  - then the weight is chosen among those faces;
  - `Substituted` when the style or the weight differs from the request.

- [ ] **Test:**
  - `WeightMatchingTests`, one test per rule: 400 with {300, 500} gives 500; 500 with {400, 600} gives 400; 300 with {200, 400} gives 200; 600 with {500, 700} gives 700; 450 with {400, 600} gives 400; an exact weight; and one face only.
  - `FontRegistryTests`, with the four Noto Sans files:
    - `AFaceTakesItsWeightAndStyleFromTheFont`, and `OverridesWin`;
    - `FamiliesIgnoreCase`;
    - `ASecondFaceWithTheSameWeightAndStyleThrows`;
    - `ItalicFallsBackToUprightAndIsSubstituted`, and the reverse;
    - `AnUnknownFamilyMatchesNothing`;
    - `AFontThatCantBeLoadedThrowsAtRegistration`, with `InvalidDataException`;
    - `StreamAndPathRegisterTheSameFace`, and the stream isn't disposed;
    - `RegistrationIsSafeFromManyThreads`: 8 threads register 8 families and match them, with no lost face;
    - `DefaultIsOneRegistry`;
    - `RefusesABlankFamilyAndAWeightOutOfRange`.
- [ ] Red, implement, the suite. Commit `#36 add the font registry and CSS weight matching`.

### Task 4: Text styles

**Files:**
- Create: `src/Maviray.Pdf/Fluent/TextStyle.cs`, `src/Maviray.Pdf/Elements/ResolvedTextStyle.cs`
- Test: `tests/Maviray.Pdf.Tests/Fluent/TextStyleTests.cs`

**Interfaces:**
- `public sealed class TextStyle`, immutable, with `TextStyle()` setting nothing. Each method returns a new style:
  - `FontFamily(string)`;
  - `FontSize(double)`, greater than 0 and at most 1,000;
  - `Weight(FontWeight)`, from 1 to 1,000, and `Bold()`;
  - `Italic(bool value = true)`, `Underline(bool value = true)` and `Strikethrough(bool value = true)`;
  - `FontColor(Color)`;
  - `LetterSpacing(double em)`, finite;
  - `LineHeight(double multiple)`, positive and finite.
  - Internal: `TextStyle Over(TextStyle parent)`, where each value not set comes from `parent`.
- `internal sealed record ResolvedTextStyle(string Family, double Size, int Weight, bool Italic, Color Color, bool Underline, bool Strikethrough, double LetterSpacing, double? LineHeight)`:
  - `static ResolvedTextStyle Resolve(TextStyle span, TextStyle element, TextStyle inherited)` resolves span over element over inherited over `BuiltIn`.
  - `BuiltIn`: `Noto Sans`, 12 pt, weight 400, upright, black, no decoration, letter spacing 0, and the font's normal line height (null).

- [ ] **Test:**
  - `EachMethodSetsOneValueAndReturnsANewStyle`;
  - `TheSpanWinsOverTheElementOverTheInheritedOverTheBuiltIn`, one value set at each level;
  - `AnUnsetValueIsInherited`;
  - `ASpanCanUndoAnInheritedUnderline`;
  - `TheBuiltInDefault`;
  - `ArgumentsAreCheckedAtTheCall`: size 0, 1,001, NaN, a negative line height, an infinite letter spacing, a blank family, weight 0.
- [ ] Red, implement, the suite. Commit `#36 add text styles and their resolution`.

### Task 5: Diagnostics

**Files:**
- Create: `src/Maviray.Pdf/Fluent/Diagnostic.cs`, `DiagnosticKind.cs`, `src/Maviray.Pdf/Layout/DiagnosticSink.cs`
- Modify: `src/Maviray.Pdf/Layout/PageContext.cs`, `LayoutEngine.cs`
- Test: `tests/Maviray.Pdf.Tests/Layout/DiagnosticSinkTests.cs`

**Interfaces:**
- `public enum DiagnosticKind { MissingGlyph, FontFaceSubstituted }`.
- `public sealed class Diagnostic`, with `Kind`, `Message`, `ElementPath` and `PageNumber`, and an internal constructor.
- `internal sealed class DiagnosticSink(Action<Diagnostic>? handler)`:
  - `void MissingGlyph(TrueTypeFont font, int codePoint, string path, int page, string message)`;
  - `void Substituted(string family, int weight, bool italic, string path, int page, string message)`.
  - Each passes a diagnostic to the handler the first time only, by font and code point, or by requested face.
- `PageContext` gains `DiagnosticSink? Diagnostics`, and `LayoutEngine.Generate` an optional `DiagnosticSink? diagnostics`, passed on in the render pass only.

- [ ] **Test:** `EachIsReportedOnce`, `DifferentFontsOrFacesAreReportedSeparately`, `ANullHandlerDropsThem`, and `OnlyTheRenderPassCarriesTheSink`, through `LayoutEngine` with a page-count element.
- [ ] Red, implement, the suite. Commit `#36 add diagnostics, reported once per document`.

### Task 6: The Text element

**Files:**
- Create: `src/Maviray.Pdf/Elements/TextElement.cs`, `TextRun.cs`, `TextRunKind.cs`
- Test: `tests/Maviray.Pdf.Tests/Elements/TextElementTests.cs`

**Interfaces:**
- `internal enum TextRunKind { Text, PageNumber, PageCount }`.
- `internal readonly record struct TextRun(TextRunKind Kind, string Text, TextStyle Style)`. `Text` is used for `Text` runs only.
- `internal sealed class TextElement(IReadOnlyList<TextRun> runs, TextStyle style, TextStyle inherited, TextAlignment alignment, FontRegistry fonts)`, named `Text`. `ShowsPageCount` when a run is `PageCount`.

**Behaviour (L-07):**
- **Preparing.** Each run's style is resolved (L-08) and its font matched (L-09), once per preparation.
  - A family that isn't registered throws `LayoutErrors.Error(page, this, "the font family '<f>' isn't registered. The registered families are <list>.", available)`, or `"... no family is registered."` when there is none.
  - For `Noto Sans` it adds: `Reference Maviray.Pdf.Fonts.Default and call RegisterNotoSans(), or register a font and make it the default style's family.`
  - Page-number and page-count runs show `PageContext`'s numbers in ASCII digits with no grouping. The paragraph is prepared again when a number it shows changes.
  - Letter spacing turns into points (em × size).
- **Filling.** At the available width. It keeps its fill at an infinite width besides its last finite fill, because a Row measures an Auto item at infinity and then at its width, again and again (P1.10 review); a request for either width reuses it.
- **Measuring.**
  - It counts the lines from its cursor whose heights sum to at most the available height plus 10⁻⁹. All of them is `Full`, some is `Partial`, and none is `None`, with the first line's size as what it needs.
  - Its width is the widest of those lines, and its height their sum.
  - A line wider than the available width plus T-09's tolerance throws `LayoutErrors.Error(page, this, "a character is <w> wide and the space is <s>.", available)`.
  - Empty text is `Full` with no size.
- **Drawing.** Each counted line at (0, its top less the first line's top), with its spans' colours. Then the cursor moves past them.
- **Diagnostics.** In the render pass, its first draw reports:
  - each missing code point, as `MissingGlyph`: `"<font> has no glyph for U+XXXX, which draws as .notdef."`;
  - each substituted face, as `FontFaceSubstituted`: `"No <italic|upright> face of weight <w> in '<family>'; <the face's family, weight and style> is used instead."`

- [ ] **Test:**
  - `ItSplitsByLine`: 30 lines of 12 points in 100 is `Partial` with 8 lines, and the next page draws from line 9 at its top;
  - `ALastLineThatFitsExactlyIsFull`;
  - `NoLineFitsIsNoneWithTheFirstLinesSize`;
  - `TheFillIsCachedByWidth`: an instrumented count, through an internal `FillCount`; measuring at infinity, at a width, and at both again fills twice;
  - `APageNumberSpanShowsTheNumberAndPreparesAgain`, and `APageCountSpanShowsTheCount`, through `LayoutEngine` and the recording canvas: the line's glyphs copy out as `Seite 1 von 2`, and `2 von 2`;
  - `ATooWideCharacterThrows`, with the message;
  - `EmptyTextIsFullWithNoSize`;
  - `TheStylesResolveAndTheFontsMatch`: a bold span is drawn in Noto Sans Bold, in its colour;
  - `AnUnknownFamilyThrows`, and `TheDefaultFamilyMissingPointsAtTheDefaultFontsPackage`;
  - `AMissingGlyphIsReportedOnceInTheRenderPass`: a CJK character in two Text elements gives one diagnostic, with the path and page;
  - `ASubstitutedFaceIsReported`: light italic with only Regular and Bold registered;
  - `ItAlignsItsLines`: a centred paragraph's lines are offset;
  - `KeepsTheContract`.
- [ ] Red, implement, the suite. Commit `#36 add the Text element`.

### Task 7: A multi-page paragraph in PDF

**Files:**
- Test: `tests/Maviray.Pdf.Tests/Elements/TextElementPdfTests.cs`

- [ ] **Test:** `AMultiPageParagraphSplitsByLineAndPassesQpdf`:
  - German prose repeated to about 200 lines, in Noto Sans, on A5 pages, with a footer of `Seite ` + page number + ` von ` + page count;
  - generated through `LayoutEngine` and `PdfCanvas`.

  It checks:
  - qpdf;
  - pdftotext, in physical-layout mode: each page's lines, in order, are the next lines of the layout, with no line lost or repeated;
  - each footer reads `Seite n von N`.
- [ ] Its red step: a mutant that draws the cursor's line twice fails the line order. Commit `#36 check a multi-page paragraph on PDF pages`.

### Task 8: Records

- [ ] The log, the index row, `#36 add the P1.11 plan and execution log`, the PR stacked on P1.10's, and the CI run.
