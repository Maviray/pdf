# P1 Layout and fluent API: design

**Status:** approved, 2026-11-23. Release 0.1.0. The third of three specs for phase P1 of the [design document](../../../design/Maviray.Pdf-Design-Document.md) (draft v0.6): fonts, then text, then layout with the fluent API. It builds on the [text spec](2026-11-23-p1-text-design.md).

## Intent

This spec makes Maviray.Pdf usable. It covers the parts that turn C# code into a multi-page document:
- the element tree;
- the layout engine with its paging loop;
- the canvas;
- the P1 elements;
- text styles and the font registry;
- the fluent API, which holds the library's first public types;
- metadata and deterministic mode;
- the `Maviray.Pdf.Fonts.Default` package.

The fonts and text specs supply what it measures and draws with.

**Gate:** the driving invoice runs to 10 pages and passes these checks (L-17):
- it is built only through the public API, from Columns and Rows;
- its header, column headings and "Seite X von 10" footer are on every page;
- every line item appears once, in order;
- its German, Turkish and Cyrillic text copies out exactly;
- it passes qpdf;
- in deterministic mode, writing it twice gives the same bytes.

## Goals, non-goals, anti-goals

**Goals**

1. A fluent API that covers FR-01 to FR-07 (justification aside), FR-09 and FR-10. Its names are fixed here (L-02).
2. Automatic paging. Any tree flows across as many pages as it needs, with a header and footer on every page and page numbers in text.
3. Content that can never fit stops the run with a `LayoutException` naming the element path (FR-09). A loop never runs forever.
4. Text in registered TrueType fonts, matched by family, weight and style. Noto Sans is one call away, from `Maviray.Pdf.Fonts.Default`.
5. Document metadata, and a deterministic mode with byte-identical output (NFR-07).

**Non-goals for this spec**

- **Later phases:** everything the roadmap puts in P2 and after, including Table, Image, Layers, keep-together and the other flow controls, components, font fallback, justified text, opacity and the canvas element.
- **Not in P1:**
  - synthetic bold and italic. A missing face falls back to the nearest one, with a diagnostic (L-09);
  - a debug mode that draws element bounds;
  - asynchronous output;
  - a balanced page tree;
  - caching of measure results beyond the Text element's own (L-07).
- **Not a product of this library:** system-font discovery (`Maviray.Pdf.Fonts.System`) and a previewer.

**Anti-goals.** This work fails, even if every test passes, if:

- a document that fits loops forever, or a document that can't fit fails without naming where;
- the same document and settings give different bytes in deterministic mode, on any platform;
- a public name copies one that is distinctive to QuestPDF (L-02);
- a type below the fluent layer becomes public, or the public API changes without its checked-in listing changing too (L-16);
- memory grows with the page count rather than with the largest page and the tree (NFR-05).

## Constraints

- Every earlier P0 and P1 constraint still holds:
  - no runtime dependencies and no native code;
  - trimming- and AOT-safe, with no reflection;
  - `net10.0` only;
  - one type per file, no partial classes, and tests first.
- **Only the fluent API is public.** The element tree, layout engine and canvas stay `internal`, as the design document's Architecture section says.
- **XML docs.** Every public type and member has XML documentation, which the build already enforces. The words in it are the words of this spec.
- **One generation, one thread.** A document is generated on one thread. Separate documents generate in parallel, and they share only the font registry and loaded fonts, which are safe to read concurrently (NFR-04).
- **Clean-room.** QuestPDF's public documentation may inform the shape of the API, and nothing else of it is used (design document, Packaging and licensing).

## Approaches considered

**Public names.** The design document keeps QuestPDF's shape and its generic layout terms, and gives the names that are distinctive to QuestPDF names of our own.

| Approach | Trade-off | Outcome |
| --- | --- | --- |
| A. Keep QuestPDF's names wherever they read well | The easiest migration. But names like `RelativeItem`, `ConstantItem`, `GeneratePdf` and `TotalPages` mark the API as a copy | Rejected |
| **B. Generic terms kept, distinctive names replaced with common .NET or CSS words** | Migration means renaming a known list of calls. The API reads as its own | **Chosen** (L-02) |
| C. A different shape altogether, such as object initializers | The most distinct. But it gives up the outside-in chaining that makes the API readable, and the migration path the design document wants | Rejected |

**Total page count.** A footer that says "Seite 3 von 10" needs the total before the first page is drawn.

| Approach | Trade-off | Outcome |
| --- | --- | --- |
| A. Reserve the total with a placeholder object filled in at the end | One pass. But a placeholder needs a width before its digits are known, and a PDF can't reflow text afterwards | Rejected |
| **B. A layout pass on a null canvas counts the pages, then the render pass draws** | Twice the layout work, but only when a page-count span exists. The count can feed back into the layout, so the passes repeat until it is stable (L-04) | **Chosen** |

**Where the Noto Sans fonts are loaded from.**

| Approach | Trade-off | Outcome |
| --- | --- | --- |
| A. The core package embeds them | Zero setup. But every user ships about 2 MB of fonts, needed or not | Rejected |
| **B. `Maviray.Pdf.Fonts.Default` embeds them, and one call registers them** | One line of setup. The core stays small, as the design document intends | **Chosen** (L-15) |
| C. The add-on registers itself when loaded | No call at all. But .NET loads an assembly only when code uses it, so it would never run without reflection or a call anyway | Rejected |

## Design

Decisions carry ids L-01 to L-18. A text decision is cited as "T-nn", a fonts decision as "F-nn", and a P0 decision as "P0 D-nn". Internal type names are working names; each plan fixes them. Public names are fixed by L-02.

### L-01 Code layout

| Layer | Folder in `src/Maviray.Pdf/` | Namespace | Visibility |
| --- | --- | --- | --- |
| Fluent API | `Fluent/` | `Maviray.Pdf` | Public |
| Element tree | `Elements/` | `Maviray.Pdf.Elements` | Internal |
| Layout engine | `Layout/` | `Maviray.Pdf.Layout` | Internal |
| Canvas | `Drawing/` | `Maviray.Pdf.Drawing` | Internal |

Dependencies point downward, as in the design document's diagram: API → elements → layout → canvas → writer, with layout → text → fonts. Tests mirror the folders.

### L-02 Public names

The names below are the public API of 0.1.0. A row marked *own* replaces a name distinctive to QuestPDF.

| Concept | Name | |
| --- | --- | --- |
| Make a document | `Document.Create(Action<DocumentBuilder> compose)` | |
| Write it | `document.Save(Stream)`, `document.Save(string path)`, `byte[] document.ToBytes()` | *own*, for `GeneratePdf` |
| Settings | `doc.Settings(s => s.Deterministic().Compression(CompressionLevel).Fonts(FontRegistry).OnDiagnostic(Action<Diagnostic>))` | |
| Metadata | `doc.Metadata(m => m.Title(..).Author(..).Subject(..).Keywords(..).Creator(..).Language("de-DE").CreationDate(DateTimeOffset))` | |
| A page set | `doc.Page(Action<PageBuilder>)` | |
| Page size | `page.Size(PageSize.A4)`, `PageSize.A4.Landscape()`, `page.Size(width, height, Unit)`. `PageSize` has A3, A4, A5, Letter and Legal | *own*, for `PageSizes` |
| Margins | `page.Margin(..)`, `MarginHorizontal`, `MarginVertical`, `MarginLeft`, `MarginTop`, `MarginRight`, `MarginBottom` | |
| Page colour | `page.Background(Color)` | *own*, for `PageColor` |
| Default style | `page.DefaultTextStyle(Func<TextStyle, TextStyle>)`, and the same on any container | |
| Slots | `page.Header()`, `page.Content()`, `page.Footer()`, each an `IContainer` | |
| Column | `.Column(c => { c.Spacing(8); c.Item().Text(..); })` | |
| Row | `.Row(r => { r.Spacing(4); r.Fixed(120).Text(..); r.Flex(2).Text(..); r.Auto().Text(..); })` | *own*: `Fixed`, `Flex` and `Auto`, for `ConstantItem`, `RelativeItem` and `AutoItem` |
| Padding | `.Padding(..)`, `PaddingHorizontal`, `PaddingVertical`, `PaddingLeft`, `PaddingTop`, `PaddingRight`, `PaddingBottom` | |
| Alignment | `.AlignLeft()`, `AlignCenter()`, `AlignRight()`, `AlignTop()`, `AlignMiddle()`, `AlignBottom()` | |
| Size limits | `.Width(..)`, `MinWidth`, `MaxWidth`, `Height`, `MinHeight`, `MaxHeight` | |
| Background | `.Background(Color)` | |
| Border | `.Border(width, Color)`, `BorderLeft`, `BorderTop`, `BorderRight`, `BorderBottom` | |
| Rule | `.HorizontalLine(thickness, Color)`, `.VerticalLine(thickness, Color)` | *own*, for `LineHorizontal` and `LineVertical` |
| Page break | `.PageBreak()` | |
| Text | `.Text(string)` returns a `TextSpanBuilder` for its style. `.Text(t => { t.Span(..).Bold(); t.PageNumber(); t.PageCount(); t.AlignCenter(); })` | *own*: `PageNumber` and `PageCount`, for `CurrentPageNumber` and `TotalPages` |
| Text style | `TextStyle` with `FontFamily`, `FontSize`, `Weight(FontWeight)`, `Bold()`, `Italic()`, `FontColor(Color)`, `Underline()`, `Strikethrough()`, `LetterSpacing(em)`, `LineHeight(multiple)` | |
| Weights | `FontWeight`: `Thin` (100) to `Black` (900) | |
| Units | `Unit`: `Point`, `Millimetre`, `Centimetre`, `Inch`. Every length parameter has an overload that takes one | |
| Colour | `Color.FromRgb(byte, byte, byte)`, `Color.FromHex("#1F4E79")` (also `#RGB`, with the `#`, in either case), `Color.Black`, `Color.White` | |
| Fonts | `FontRegistry.Default`, `new FontRegistry()`, `registry.Register(family, Stream / byte[] / string path, FontWeight? weight, bool? italic)` | |
| Default fonts | `FontRegistry.RegisterNotoSans()`, an extension method in `Maviray.Pdf.Fonts.Default` | |
| Errors | `LayoutException`, with `ElementPath`, `AvailableWidth`, `AvailableHeight` | |
| Diagnostics | `Diagnostic`, with `Kind` (`MissingGlyph`, `FontFaceSubstituted`), `Message`, `ElementPath` and `PageNumber` | |

- **Lengths** are `double`, in points unless a `Unit` says otherwise.
- **Builders** are sealed classes. `IContainer` is an interface with no members, and only the library implements it. The extension methods check this and throw `ArgumentException` for any other implementation.
- **Arguments are checked at the call:** a negative length, a size or weight out of range, a malformed colour or language tag. Each throws `ArgumentException` or `ArgumentOutOfRangeException`, naming the parameter.
- **Builders are single-use.** A container takes one child. Giving it a second one throws `InvalidOperationException`.

### L-03 Element protocol

An element is measured, then drawn. The design document's working names (`SpacePlan`, `Wrap`, `PartialRender`, `FullRender`) are QuestPDF's own internal ones, so the engine uses its own:

- `Measure(available)` returns a fit and a size. The fit is `Full` (everything left fits), `Partial` (part fits, and the rest continues on the next page) or `None` (nothing fits here).
- `Draw(available, canvas)` draws exactly what `Measure` said fits at that size, and advances the element's state.
- `Reset()` returns the element to its initial state before a pass.

**The contract**, checked by tests on every element type:
- `Measure` has no side effects, and the same state and size give the same answer.
- A returned size never exceeds the available size.
- After `Partial`, `Draw` advances at least one unit: a line, an item or a child.
- `None` carries no size.
- Available sizes are finite. One exception: the width may be infinite when a Row measures an `Auto` item (L-06). Every element handles that.

All lengths are `double`. The design document's `float` is amended (draft v0.6).

### L-04 Pages and passes

**Page sets.** Each `doc.Page(..)` is a page set. Its content flows across as many pages as it needs, and the next page set starts on a new page. Page numbers run through the whole document.

**One page of a set.**
1. The background, if set, fills the page.
2. The header and footer are measured at the inner width, which is the page minus the margins. Each must fit fully. Otherwise the run throws `LayoutException`, because they repeat on every page.
3. The content gets the height between them and is measured.
   - **Full:** it is drawn and the set ends.
   - **Partial:** it is drawn, and a new page follows.
   - **None:** on a page where the content hasn't drawn anything yet, it can never fit, and the run throws `LayoutException`. Otherwise a new page follows.
4. The header and footer are drawn, and then reset, so they draw again on the next page.

A `Draw` after `Partial` that advances nothing also throws `LayoutException`, naming the element. That and the empty-page rule are what stop every loop.

**Passes.** Without a `PageCount` span there is one pass, the render pass. With one, layout passes run on the null canvas first:
1. In the first layout pass, `PageCount` shows the current page number.
2. In each later layout pass, it shows the page count the pass before ended with.
3. The layout passes stop when one ends with the count it showed, which takes at least two. If the third still doesn't, the run throws `LayoutException`, because the count keeps changing the layout.
4. `Reset()` then runs on the whole tree, and the render pass draws with that count.

The layout pass encodes no glyphs and writes nothing.

**Memory.** Each page goes to the output when it ends (P0 D-06). What generation holds is the element tree, the fonts and the current page (NFR-05).

### L-05 Element paths and errors

Every element has a name. Its path is the names from the page down, for example `Page 3 > Content > Column > Item 2 > Row > Flex 1 > Text`.
- Items count from 1.
- A Row's items are named by their kind and their position among all of the row's items: its second item, if it is a flexible one, is `Flex 2`.
- A slot is named `Header`, `Content` or `Footer`.

`LayoutException`'s message names three things:
- the innermost element that can't fit;
- the reason;
- the available size, and the size the element needs when it is known.

For example: `Page 3 > Content > Column > Item 2 > Row > Fixed 1 > Text: a character is 31.2 pt wide and the space is 20 pt.` The exception is the only one generation throws for content. Argument errors throw at the call (L-02), and a font that can't be loaded throws when it is registered (L-09).

### L-06 Containers

| Element | Measure | Draw |
| --- | --- | --- |
| Column | Measures items in order from its cursor, adding `Spacing` between items on the same page. It stops at the first `Partial` or `None`, and is `None` only when its first remaining item is. Its width is the widest item's | Each item gets the full width. Finished items never draw again |
| Row | Widths in order: `Fixed`, then `Auto` (the item measured at infinite width), then `Flex` shares of what is left, with `Spacing` between items. Its height is the tallest unfinished item's. It is `None` if any item is, and `Partial` if any is. Fixed and auto widths that exceed the space throw `LayoutException` | Every item draws its part at the row's height. An item that finished on an earlier page draws nothing |
| Padding | The child gets the space minus the padding. A space of zero or less means `None` | The child is offset by the padding |
| Align | The child is measured at the space | The child is drawn at its measured size, placed left, centre or right, and top, middle or bottom. On a `Partial`, it stays at the top |
| Width, MinWidth, MaxWidth | Clamp the child's width: `Width(w)` gives exactly w, and is `None` when the space is narrower | As measured |
| Height, MinHeight, MaxHeight | `MaxHeight(h)` gives the child at most h on each page. `MinHeight(h)` makes each page's part at least h tall, and is `None` when less than h is left. `Height(h)` is both | As measured |
| Background | As its child | Fills the space it is drawn in, the width given and the child's height, then draws the child, on every page part |
| Border | The child gets the space minus the border widths, so the border takes space, as in CSS | Draws the child, then fills each side's band in the colour |
| Horizontal line | Width 0, height the thickness | Fills the width given |
| Vertical line | Width the thickness, height 0 | Fills the height given, which in a Row is the row's height |
| Page break | `Partial` with no size the first time, so the content ends on this page. Then `Full` with no size | Marks itself as passed, which counts as progress |

At infinite width, a Row measures its `Flex` items like `Auto` items, a horizontal line has width 0, and `Width(w)` gives w.

### L-07 Text element

The Text element holds spans, each with a style, and the paragraph's alignment. It uses the text engine (T-14):
- **Preparing.** It resolves each span's style (L-08) and font (L-09), and prepares the paragraph once. A paragraph with a `PageNumber` or `PageCount` span is prepared again whenever the number it shows changes. The numbers are written in ASCII digits, with no grouping.
- **Filling.** It fills at the available width, and keeps the last fill and its width. A request for the same width reuses it.
- **Measuring.** It counts the lines from its cursor that fit the available height. All of them is `Full`, some is `Partial`, and none is `None`. Its width is the widest line among them, and its height is their sum. Lines are the unit of splitting across pages.
- **Drawing.** It draws those lines and moves its cursor past them.
- **Too wide.** A line wider than the available width plus the engine's tolerance (T-09) holds a single cluster that can never fit. It throws `LayoutException`.
- **Empty text** measures as `Full` with no size.
- **Missing characters.** Each code point the font lacks (T-07) raises a `MissingGlyph` diagnostic in the render pass, once per font and code point.

### L-08 Text styles

`TextStyle` is immutable. Each method returns a new style, and a value that isn't set is inherited. A span's style resolves from the nearest setting, in this order:
1. the span;
2. the Text element;
3. the nearest container `DefaultTextStyle`;
4. the page's `DefaultTextStyle`;
5. the built-in default: family `Noto Sans`, 12 pt, weight 400, not italic, black, no decoration, letter spacing 0, and the font's normal line height.

Values convert into the text engine's style (T-14):
- the size is in points, greater than 0 and at most 1,000;
- letter spacing is a fraction of the size (em), turned into points;
- line height is a multiple of the size.

### L-09 Fonts

**Registry.**
- `Register(family, data, weight, italic)` loads the font at once (F-02), so a font that can't be used fails at registration with its `NotSupportedException` or `InvalidDataException`.
- Without overrides, a face's weight and style come from the font: `usWeightClass`, and the italic flag (F-03).
- Families are compared by ordinal, ignoring case.
- Registering a second face with the same family, weight and style throws `ArgumentException`.
- A registry is safe to use from many threads, and `FontRegistry.Default` is the process-wide one. A document can use another registry through its settings, which is how tests stay independent.

**Matching**, done once per span style:
- **Family.** A family that isn't registered throws `LayoutException`, naming it and listing the registered families. When it is the default `Noto Sans`, the message says to reference `Maviray.Pdf.Fonts.Default` and call `RegisterNotoSans()`, or to register a font and set it as the default style's family.
- **Style.** An italic request uses the family's italic faces, if it has any. Otherwise it uses the upright faces and raises `FontFaceSubstituted`. An upright request works the same way the other way round.
- **Weight.** Among those faces, the weight is matched by the CSS Fonts Level 4 rules:
  - for 400, try 500 next, then lighter, then heavier;
  - for 500, try 400 next, then lighter, then heavier;
  - below 400, try lighter first, then heavier;
  - above 500, try heavier first, then lighter.
- **Substitution.** A different weight raises `FontFaceSubstituted` too. Nothing is synthesized in P1.

### L-10 Canvas

The canvas draws in points, with the origin at the top left of the page and y downward.
- **Operations:** translate (saved and restored), fill a rectangle in a colour, and draw a line of a text layout (T-14) at a position, with each span's colour.
- **Null canvas:** for the layout pass. It draws nothing and encodes nothing.
- **Recording canvas:** for tests. It records every call with its absolute position, so layout tests assert positions without a PDF.
- **PDF canvas:** it maps onto `ContentStreamBuilder`.
  - y becomes the page height minus y.
  - Each glyph gets its CID from `EmbeddedFont.Encode` (F-07), and each glyph run is one `Tm` and one positioned `ShowGlyphs` (T-13).
  - Decorations are filled rectangles.
  - Each font is embedded into the document once, through `PdfDocumentWriter.EmbedFont` (F-10).

### L-11 Settings and output

| Setting | Default | Effect |
| --- | --- | --- |
| `Deterministic()` | Off | L-13 |
| `Compression(CompressionLevel)` | `Optimal` | P0 D-08 |
| `Fonts(FontRegistry)` | `FontRegistry.Default` | L-09 |
| `OnDiagnostic(Action<Diagnostic>)` | None: diagnostics are dropped | Called on the generating thread, during the render pass |

**Each `Save`:**
- runs the compose callback again, builds a new tree and generates it, so a `Document` can be saved many times and from many threads;
- `Save(Stream)` writes to the caller's stream and never disposes it;
- `Save(string path)` writes a temporary file beside the target and moves it into place when generation succeeds. A failure deletes the temporary file and leaves any existing file unchanged;
- `ToBytes()` returns the file.

A document with no page sets throws `InvalidOperationException`.

### L-12 Metadata

The writer gains the Info dictionary, the catalog's `/Lang` and the viewer preference that shows the title.
- **Info dictionary:**
  - `/Title`, `/Author`, `/Subject`, `/Keywords` and `/Creator` as text strings (P0 D-02);
  - `/Producer` as `Maviray.Pdf <version>`, with the package version and no build metadata;
  - `/CreationDate` in the PDF date format with its UTC offset, for example `D:20261123140500+01'00'`.
- **When each is written:** values that aren't set aren't written. Without deterministic mode, `CreationDate` defaults to the time `Save` starts.
- **Language.** `Language` is a BCP 47 tag, checked only for its syntax: letters and digits in hyphen-separated parts of 1 to 8 characters. It is written as the catalog's `/Lang`.
- **Title display.** When a title is set, the catalog's `/ViewerPreferences` holds `<</DisplayDocTitle true>>`, so viewers show the title, not the file name.

### L-13 Deterministic mode

- The trailer's two `/ID` strings are the first 16 bytes of the SHA-256 of every byte written before the trailer. This replaces the random identifier of P0 D-07.
- `CreationDate` is written only when the caller sets it.
- Everything else is already deterministic:
  - subset tags (F-09);
  - number formatting (P0 D-03);
  - the text engine (T-14);
  - the order objects are written in.

Writing the same document twice, with the same settings and the same version of the library, gives identical bytes on every platform.

### L-14 Diagnostics

A diagnostic reports something that went wrong without stopping the run. It carries its kind, a message, the element path and the page number, and goes to the `OnDiagnostic` handler. In P1 there are two kinds:
- `MissingGlyph`, from L-07;
- `FontFaceSubstituted`, from L-09.

Each is raised at most once per document for the same font and code point, or the same requested face.

### L-15 `Maviray.Pdf.Fonts.Default`

**The project:** `src/Maviray.Pdf.Fonts.Default/Maviray.Pdf.Fonts.Default.csproj` joins the solution, so `dotnet pack` builds its package. It depends on the `Maviray.Pdf` package of the same version.
- **Its fonts:** the four Noto Sans files already in `Fonts/` (F-12), compiled in as embedded resources.
- **Its one public member:** `RegisterNotoSans(this FontRegistry registry)` registers the family `Noto Sans` with its four faces and returns the registry.
- **Its package** carries `OFL.txt`, `THIRD-PARTY-NOTICES.md` and `LICENSE`.

`Maviray.Pdf`'s own tests keep finding the fonts as files (F-12). The package's own test loads them from its resources, and compares each with its file.

### L-16 Public API listing

A test lists the public types and members of both assemblies, with their signatures, in a stable order. It compares that list with a checked-in file per assembly. Any difference fails, so a change to the public API is always visible in review. With `MAVIRAY_PDF_WRITE_PUBLIC_API` set to `true`, the test writes the files instead.

### L-17 Driving invoice gate

The driving invoice of the design document lives in `tests/Maviray.Pdf.Tests/Invoice/`, with synthetic data and a document built only through the public API.

**What it holds:**
- a header with the seller's name, the invoice number and date, and the line-item column headings, repeated on every page (no logo, which is P2);
- the seller's and buyer's addresses side by side;
- line items as a Column of Rows: position, description, quantity, unit price, VAT rate and amount;
- totals at the end;
- a footer with "Seite X von Y", the bank details and the company registration;
- German throughout, with Turkish and Cyrillic names in the buyer data and item descriptions.

The data is sized so the document runs to exactly 10 pages.

**It passes when:**
- the document has 10 pages;
- `pdftotext` of each page returns the header, the column headings and `Seite <n> von 10`;
- across the pages, every line item's description appears exactly once and in order, and the totals appear on the last page;
- every Turkish and Cyrillic string copies out exactly;
- `qpdf --check` reports no errors, and `pdffonts` reports every font as embedded, subset and Unicode-mapped;
- `pdftoppm` renders no ink outside the page's margins;
- in deterministic mode, writing it twice gives identical bytes.

The viewer checklist gains the invoice file, with its description and a clipboard check of page 1. Its check by hand joins the 0.1.0 release check, like the other gate files.

### L-18 Documentation and delivery

As F-16 and T-16. The repository's `README.md` gains a getting-started example that builds a small document with the public API. A test runs the same code and checks that the README's code block matches it character for character, so the example can't go stale. This spec and the design-document amendments (draft v0.6) ship in one docs pull request:
- the element protocol's names and `double` lengths;
- the public names of L-02, replacing the working names in the design document's example;
- Text as the only cached element in P1;
- no synthetic faces in P1.

## Work items

They run in order, because each builds on the one before.

| Item | Scope | Tests pin | Done when |
| --- | --- | --- | --- |
| **P1.9** Layout engine and canvas | L-03 to L-05, L-10 | The protocol contract; the paging loop with test elements of fixed size; header and footer on every page; no-progress and empty-page errors with their paths; the passes and their limit; canvas positions and the y flip | A test tree of fixed blocks pages correctly, and every error names its element |
| **P1.10** Containers | L-06 | Each row of L-06, at its edges: spacing across pages, row widths in each order, an item finished early, a too-wide row, padding larger than the space, every alignment, each size limit, borders and backgrounds on every page part, the page break, infinite width | Each container's layout matches the recording canvas's expected positions |
| **P1.11** Text element, styles and fonts | L-07 to L-09, L-14 | Splitting by line, the cached fill, page-number spans re-prepared, a too-wide character, style resolution at every level, weight matching for every rule, a missing family, substitution diagnostics, concurrent registration | A multi-page paragraph splits by line and every page passes qpdf |
| **P1.12** Fluent API, settings and metadata | L-01, L-02, L-11 to L-13, L-16, L-18 | Every public name; argument checks; single-use builders; a foreign `IContainer`; `Save` to a stream, a path (and deleted on failure) and bytes; the Info dictionary and `/Lang`; deterministic bytes and the hashed `/ID`; the API listing | A document written with the public API passes qpdf, and the README example runs |
| **P1.13** Default fonts package | L-15 | Registration of the four faces; the resources match the files; the package's contents | `dotnet pack` builds both packages, and a document with only `RegisterNotoSans()` set up passes qpdf |
| **P1.14** Invoice gate | L-17 | The gate test with every check | The gate test passes in CI, and the checklist describes the invoice |

## Left to the builder

- Internal type and member names, and how elements hold their state.
- How the engine finds the innermost element for an error (L-05).
- The invoice's data and exact layout, within L-17.
- The exact wording of messages, which plans pin in tests.
