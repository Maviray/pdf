# P0 Foundations: design

**Status:** draft for approval, 2026-11-19. Release 0.1.0. Implements phase P0 of the [design document](../../../design/Maviray.Pdf-Design-Document.md) (draft v0.3).

## Intent

P0 builds the bottom of the stack: the PDF object model, a streaming file writer, a content-stream builder, and text in the standard Helvetica font. Every later layer writes through this code, so P0 has to prove that the files it produces are correct by the judgement of tools we didn't write, before any layout is built on top of it.

**Gate:** a hello-world file opens cleanly in Acrobat, Chrome and Firefox, and qpdf reports no errors. The design document also named macOS Preview. No Mac is available, so Preview moves to the 0.1.0 release check (D-13).

## Goals, non-goals, anti-goals

**Goals**

1. Every PDF object type serializes to the exact bytes the PDF specification requires.
2. The writer streams: each object goes to the output when it is complete, and a finished page is flushed and released.
3. The content-stream builder emits rectangles, colours, line widths and Helvetica text, and skips redundant state changes.
4. Every generated file in the test suite is checked by `qpdf --check`, rendered by `pdftoppm`, and, where it holds text, read back by `pdftotext`.
5. CI installs those tools and fails if they are missing, so no check is skipped silently.

**Non-goals for P0**

- Any public type. The fluent API and the first public types arrive in P1.
- Layout, the canvas and its top-left origin, TrueType fonts, images, metadata (the Info dictionary), deterministic mode, PDF 2.0, cross-reference streams and encryption.
- A balanced page tree. P0 documents have one page; see D-06.
- Unicode text beyond WinAnsi, and NFC normalization. Both come with the P1 text pipeline.
- Automated rendering checks in Chrome or Firefox, and visual comparison against approved images (P2).

**Anti-goals.** P0 fails, even if every test passes, if:

- a file passes our tests but not qpdf;
- `src/` gains a package reference or a public type;
- CI skips a validator without failing.

## Constraints

- No runtime dependencies, no native code, no P/Invoke (NFR-01). The existing dependency-guard test keeps passing; `System.IO.Compression` and `System.Security.Cryptography` ship with the runtime.
- Trimming- and AOT-safe (NFR-02): no reflection and no dynamic code. The analyzers already enforce this, and warnings fail the build.
- `net10.0` only (NFR-03). One type per file, and no partial classes.
- Tests first: every behaviour lands with the test that pins it, red before green.
- Clean-room: the PDF specification (ISO 32000) is the only reference for file structure and operators.

## Approaches considered

How the external validators check generated files:

| Approach | Trade-off | Outcome |
| --- | --- | --- |
| **A. The tests call the validators.** A test helper saves each generated PDF and runs qpdf and poppler on it. | A failure names the test that made the file. Locally, a missing tool skips the check with a message; in CI it fails the run. | **Chosen** |
| B. A CI step checks a folder of PDFs after the tests. | The tests stay pure .NET, but a failure names a file, not a test, and nobody runs the check locally. | Rejected |

The object model and the typed content-stream builder were already decided in the design document, so they had no alternatives here.

## Design

Decisions carry ids, so a later change can name the decision it replaces. Type names are working names; each plan fixes them.

### D-01 Code layout and visibility

All P0 code lives in `src/Maviray.Pdf/Writer/`, in the namespace `Maviray.Pdf.Writer`, and every type is `internal`. `Maviray.Pdf.csproj` declares `<InternalsVisibleTo Include="Maviray.Pdf.Tests" />`. Test code mirrors the layout in `tests/Maviray.Pdf.Tests/Writer/`, and the validator helpers go in `tests/Maviray.Pdf.Tests/Validation/`.

### D-02 Object model and serialization

An abstract `PdfObject` has one sealed subclass per PDF type. Each subclass writes itself straight into a `PdfOutput` (D-04), with no intermediate strings. Keywords and delimiters are UTF-8 literals (`"obj"u8`).

| Type | Holds | Written as |
| --- | --- | --- |
| `PdfNull` | One shared instance | `null` |
| `PdfBoolean` | `bool`; two shared instances | `true`, `false` |
| `PdfInteger` | `long` | Decimal digits with an optional minus sign |
| `PdfReal` | `double` | D-03 |
| `PdfName` | Non-empty UTF-8 bytes | `/` followed by the bytes. `#XX` (uppercase hex) for any byte outside `0x21`–`0x7E`, for `#`, and for the delimiters `( ) < > [ ] { } / %` |
| `PdfString` | Bytes, plus a form: literal or hex | Literal: `(…)`, escaping `\` `(` `)` as `\\` `\(` `\)`, CR, LF, TAB, BS and FF as `\r` `\n` `\t` `\b` `\f`, other bytes below `0x20` and `0x7F` as three-digit octal `\ddd`, and every other byte as is. Hex: `<…>` in uppercase pairs. |
| `PdfArray` | A list of objects | `[a b c]`, with single spaces |
| `PdfDictionary` | Name–object pairs in insertion order | `<</Key value /Key value>>`. Setting an existing key replaces the value in place. A `PdfNull` value is refused; remove the key instead. |
| `PdfStream` | A dictionary and the data bytes | D-08 |
| `PdfReference` | Object number; generation is always 0 | `12 0 R` |

`PdfString.FromText(string)` gives the text-string form: bytes `0x20`–`0x7E` when every character is printable ASCII, otherwise UTF-16BE with the `FE FF` byte-order mark, written as hex. Well-known names (`Type`, `Page`, `Pages`, `Catalog`, `Font`, …) are shared static instances.

### D-03 Numbers

One formatter serves `PdfReal` and the content-stream builder:

- invariant culture, whatever the current culture;
- rounded to at most four decimals, midpoints away from zero;
- no exponent, no trailing zeros, no trailing decimal point, and a leading `0` before the point (`0.5`);
- `-0` is written as `0`;
- NaN and infinities are refused when the value is created.

### D-04 Output

`PdfOutput` wraps the caller's `Stream`. It buffers into a pooled array, counts every byte it writes, and writes the buffer to the stream when it is full and at the end. The writer never seeks and never reads `Stream.Position`, so it works with streams that can't seek, such as an HTTP response. The caller owns the stream: the writer doesn't dispose it.

### D-05 File structure and write order

The file writer follows the write order in the design document:

1. Header: `%PDF-1.7`, LF, then `%` followed by the bytes `E2 E3 CF D3`, LF. Every line in the file ends with LF alone.
2. Indirect objects: `<n> 0 obj`, LF, the value, LF, `endobj`, LF. Object numbers are handed out in order by `Reserve()`. The byte offset of each object is recorded when it is written.
3. Every reserved object is written exactly once. Writing one twice, or closing with one unwritten, throws `InvalidOperationException` and names the object numbers.
4. Cross-reference table: `xref`, then `0 <Size>`, then one 20-byte entry per object: `0000000000 65535 f`, then `nnnnnnnnnn 00000 n` for each object, each entry ending with a space and LF. An offset that doesn't fit in ten digits throws.
5. Trailer: `trailer`, LF, `<</Size n /Root r /ID [<…> <…>]>>`, LF, `startxref`, LF, the offset of `xref`, LF, `%%EOF`, LF.

### D-06 Document and pages

A `PdfDocumentWriter` sits on top of the file writer. The working API:

```csharp
using var document = new PdfDocumentWriter(output, options);
var page = document.BeginPage(width: 595.2756, height: 841.8898);   // A4 in points
page.Content.SetFillColor(new RgbColor(0.2, 0.4, 0.8)).Rectangle(72, 600, 200, 100).Fill();
document.EndPage(page);
document.Close();
```

- The page tree's object number is reserved when the writer is created, so each page dictionary can point at its `/Parent` straight away.
- One page is open at a time. `EndPage` checks that the content stream is balanced (D-09), then writes the content stream, then the page dictionary: `/Type /Page`, `/Parent`, `/MediaBox [0 0 w h]`, `/Resources` holding only the fonts that page uses, and `/Contents`. It then releases the page's buffers. A page with no content gets no `/Contents` entry.
- `Close` writes the shared resources (the Helvetica font dictionary, once), the page tree (`/Type /Pages /Kids [...] /Count n`), the catalog (`/Type /Catalog /Pages`), the cross-reference table and the trailer. Closing with no pages throws.
- The page tree is flat in P0. The balanced tree from the design document is deferred until a document has enough pages to need it.
- `Dispose` returns pooled buffers. Disposing without `Close` leaves an incomplete file and doesn't throw.

### D-07 File identifier

The trailer `/ID` holds two identical 16-byte strings from `RandomNumberGenerator`. `PdfWriterOptions.FileId` lets a test pass a fixed value. A hash-based ID belongs to deterministic mode in P1.

### D-08 Streams and compression

The writer owns `/Length` and `/Filter`. A `PdfStream` whose dictionary already has either one is refused. The writer compresses stream data with `ZLibStream` at `PdfWriterOptions.CompressionLevel` (default `Optimal`) and adds `/Filter /FlateDecode`. With `CompressionLevel.NoCompression`, it writes the data as it is, with no filter, so a test or a developer can read a content stream in the file. The data is followed by `stream`, LF, the bytes, LF, `endstream`.

### D-09 Content-stream builder

`ContentStreamBuilder` writes operators into a pooled buffer, in PDF user space: points, with the origin at the bottom left. The top-left origin belongs to the P1 canvas. Operands are separated by single spaces, and each operator ends the line.

| Method | Operator | Work item |
| --- | --- | --- |
| `SaveState`, `RestoreState` | `q`, `Q` | P0.3 |
| `Transform(a, b, c, d, e, f)` | `cm` | P0.3 |
| `SetLineWidth(w)` | `w` | P0.3 |
| `SetFillColor(rgb)`, `SetStrokeColor(rgb)` | `rg`, `RG` | P0.3 |
| `Rectangle(x, y, w, h)` | `re` | P0.3 |
| `Fill`, `Stroke`, `FillAndStroke` | `f`, `S`, `B` | P0.3 |
| `BeginText`, `EndText` | `BT`, `ET` | P0.4 |
| `SetFont(font, size)` | `Tf` | P0.4 |
| `SetTextMatrix(a, b, c, d, e, f)` | `Tm` | P0.4 |
| `ShowText(text)` | `Tj` | P0.4 |

`TJ` arrives with positioned glyphs in P1. `Do` and `gs` arrive with images and opacity in P2.

**State tracking.** The builder tracks fill colour, stroke colour, line width, and font and size. `q` saves them and `Q` restores them. Setting a value that is already current writes nothing. Tracking starts unknown, so the first setting on a page is always written.

**Sequencing.** Anything the PDF specification doesn't allow throws `InvalidOperationException`:

- path or `q`/`Q`/`cm` operators inside `BT`…`ET`;
- text operators outside it, and nested `BT`;
- `ET` without `BT`, and `Q` without `q`;
- `ShowText` before `SetFont`;
- painting without a path;
- colour or state operators between building a path and painting it;
- ending a page with an open `q` or `BT`.

`RgbColor` is an internal record struct with components from 0 to 1. The public `Color` type in P1 maps onto it.

### D-10 Helvetica and WinAnsi

`StandardFont.Helvetica` is the only font in P0. Its dictionary is `<</Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding>>`. It's written once at `Close`, under a reference reserved on first use, and named `/F1` in each page's resources. It has no `/Widths` and no font descriptor, which PDF 1.7 permits for the standard 14 fonts. The TrueType fonts in P1 carry both.

`WinAnsiEncoding` maps Unicode to bytes with a 256-entry table taken from Annex D of the PDF specification: ASCII, the Windows-1252 characters in `0x80`–`0x9F` (`€` is `0x80`), and Latin-1 in `0xA0`–`0xFF`. It doesn't use `System.Text.Encoding.CodePages`, which needs a process-wide registration. A character outside the table, including control characters, throws `ArgumentException` naming the character and its position. `ShowText` writes the encoded bytes as a literal string with the escaping from D-02.

### D-11 Validators in the tests

The test project gets three helpers, one per tool, that run the tool on a PDF the test saved:

| Helper | Runs | Passes when |
| --- | --- | --- |
| `Qpdf.Check(path)` | `qpdf --check <file>` | Exit code 0. Warnings (exit code 3) fail as well as errors (exit code 2). |
| `Poppler.Render(path)` | `pdftoppm -r 72 <file> <prefix>` | Exit code 0 and nothing on stderr, since poppler reports syntax errors there but may still exit 0. It returns one parsed PPM image per page; at 72 dpi one point is one pixel. |
| `Poppler.ExtractText(path)` | `pdftotext -enc UTF-8 <file> -` | Exit code 0. It returns the text, and the test compares it with the source text. |

- **Finding the tools.** The helpers look for the tools on `PATH` (with `.exe` on Windows). When one is missing, the check calls `Assert.Skip` with the tool's name and how to install it. If `MAVIRAY_PDF_REQUIRE_VALIDATORS` is `true`, a missing tool fails the test instead. CI sets it.
- **Running the tools.** Each run waits for the process to exit, with a 60-second timeout, and kills it on timeout. A failure message includes the tool's full output.
- **Where files go.** Generated PDFs and renders are saved under `<test output folder>/generated/<test class>/<test method>.*`. They are overwritten on each run, and git already ignores them, because they are under `bin/`.
- **Blind spot.** `ExtractText` drops trailing whitespace and form feeds before comparing. It can't see differences in either, and the comment next to it says so.
- **PPM.** The PPM parser is a small test type. pdftoppm's default output format is binary PPM (`P6`), so no image library is needed.

**Rules for byte-level tests:**

- Expected bytes are written with explicit `\n`, never as multi-line string literals. On Windows, git checks the source files out with CRLF, and a multi-line literal would carry those line endings.
- Compressed output is decompressed before comparing. Flate output isn't compared byte for byte.
- One number-format test runs under a culture with a decimal comma (`de-DE`).

### D-12 CI

`build.yml` and `publish-nuget.yml` both install the validators before the test step, `sudo apt-get update` then `sudo apt-get install -y --no-install-recommends qpdf poppler-utils`. Both print the tool versions and set `MAVIRAY_PDF_REQUIRE_VALIDATORS: true` on the test step, so a release can't skip a check either.

`build.yml` also uploads the `generated` folders as the artifact `generated-pdfs`. The upload runs even when tests fail, and the artifact is kept for 14 days. The upload action is pinned to a full commit SHA, like the other actions. The workflow keeps its read-only permissions.

`CONTRIBUTING.md` says that the tests check generated PDFs with qpdf and poppler when they are installed, and that CI requires them.

### D-13 Hello-world gate and the viewer check

A hello-world test writes an A4 page with "Hello, world" in Helvetica 24 pt, a line of WinAnsi text (`Grüße aus Köln – 25 €`), a filled rectangle and a stroked one. It runs all three validators: qpdf, a pixel check of both rectangles in the render, and text extraction.

The viewers are checked by hand, because Acrobat and Preview can't run in CI:

- **The checklist** is `docs/qa/checklists/viewer-check.md`, a living doc. It is never ticked in place. For each viewer it checks:
  - the file opens without a warning;
  - the page matches the render in the artifact;
  - the text selects and copies out exactly;
  - Acrobat doesn't offer to save changes when the file closes. That offer is Acrobat's sign that it repaired the file.
- **Each run** is a dated copy under `docs/qa/reports/`. It records the viewer versions, the operating system, the CI run the artifact came from, and a verdict per viewer.
- **The checklist lists all four viewers.** A run records each one as pass, fail or not checked, with the reason.
- **The P0 gate passes** when the first report has a pass for Acrobat, Chrome and Firefox. Preview is recorded as not checked, because no Mac is available.
- **The 0.1.0 release check** runs the checklist again, with Preview if a Mac is available by then. If not, the release report records Preview as not checked.

### D-14 Documentation and delivery

- **Docs.** Release documents go under `docs/releases/0.1.0/` (see [docs/README.md](../../../README.md)). `.gitignore` re-includes `docs/releases/`, because the upstream template ignores every `releases/` folder. Session handoffs stay out of the repository.
- **Issues.** Each work item below is one GitHub issue in the milestone `v0.1.0`.
- **Plans and logs.** The plan for an item is written when the item starts, as `plans/YYYY-MM-DD-issue-<n>-<slug>.md`. Its execution log goes in `execution-logs/`. Both ship in the item's pull request.
- **Branches, commits and PRs.** Each item is one branch `feature/<n>-<slug>` from `develop` and one pull request into `develop`. Commit subjects and PR titles are `#<n> <imperative summary>`, and the body says `Refs #<n>`. The maintainer merges.
- **Closing issues.** GitHub's closing keywords only fire on the default branch, which is `main`, so each issue is closed by hand after its merge, with a link to the pull request.
- **Release index.** Each PR sets its own row in the release index to "in review". The next PR records the merge: date and PR number.

## Work items

They run in order, because each builds on the one before.

| Item | Scope | Tests pin | Done when |
| --- | --- | --- | --- |
| **P0.1** PDF object model and serialization | D-01 to D-04: every `PdfObject` type, `PdfOutput`, the number formatter, `InternalsVisibleTo` | The exact bytes for every type and escaping rule; number edge cases (`-0.00004` → `0`, `0.00005` → `0.0001`, NaN refused, `de-DE` culture); a stream that can't seek | All serialization tests pass in CI |
| **P0.2** Streaming file writer, checked by qpdf and poppler | D-05 to D-08, D-11 and D-12: file writer, document writer with blank pages, `/ID`, Flate, validator helpers, CI install, artifact upload, CONTRIBUTING | Every xref offset points at its `n 0 obj`; `startxref` points at `xref`; an unwritten or twice-written object throws; a blank one-page file passes qpdf and renders as one white page of the right size; a Flate stream decompresses to its input | A blank A4 file passes `qpdf --check` in CI, and the artifact holds it |
| **P0.3** Content-stream builder, with rectangles | D-09 graphics operators, page `/Contents` | The bytes of each operator; redundant settings skipped; `q`/`Q` restoring the tracked state; every sequencing error; pixel colours inside and outside filled and stroked rectangles in the render | Rectangles render where and in the colour the test expects |
| **P0.4** Helvetica text | D-09 text operators, D-10 | The WinAnsi table (ASCII identity, `€`→`0x80`, `ü`→`0xFC`, undefined codes such as `0x81` never produced, unmapped characters refused); the font dictionary written once and listed in the resources of each page that uses it; `pdftotext` returning ASCII and Latin-1 text exactly | `pdftotext` returns the source text exactly |
| **P0.5** Hello-world gate and the first viewer check | D-13 | The hello-world test with all three validators | The first viewer-check report passes Acrobat, Chrome and Firefox |

## Left to the builder

- Type and member names beyond those fixed above. Each plan settles them.
- The size of the output buffer, and the internal shape of the state stack.
- The exact winget, Homebrew or apt package names in `CONTRIBUTING.md`, checked when written.
- The current release of `actions/upload-artifact`, and its commit SHA.
