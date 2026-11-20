# P1 Fonts: design

**Status:** draft for approval, 2026-11-20. Release 0.1.0. The first of three specs for phase P1 of the [design document](../../../design/Maviray.Pdf-Design-Document.md) (draft v0.4): fonts, then text, then layout with the fluent API.

## Intent

P1 replaces Helvetica with real fonts. This spec covers reading a TrueType font, building the subset a document uses, and embedding it so every character prints, copies out and searches correctly. The text engine and the layout engine both measure with these fonts, so this work comes first.

**Gate:** a PDF with German, Turkish, Azerbaijani, Russian, Ukrainian and Greek lines in the four Noto Sans styles passes qpdf, every font in it is reported as embedded, subset and Unicode-mapped, and `pdftotext` returns every line exactly (F-15).

## Goals, non-goals, anti-goals

**Goals**

1. Read static TrueType fonts (`glyf` outlines): metrics, advance widths, and the Unicode `cmap` in formats 4 and 12.
2. Refuse every font the library can't embed correctly with a clear `NotSupportedException`, and every malformed font with an `InvalidDataException`. No other exception type escapes, and no input hangs.
3. Build a subset with only the glyphs a document uses, and check it with fontTools, a tool we didn't write.
4. Embed it as a Type 0 font with a ToUnicode map, so text copies out exactly in every viewer.

**Non-goals for this spec**

- Text measuring, line breaking and NFC normalization. They belong to the text spec.
- Any public type: `FontRegistry`, matching by family, weight and style, and the `Maviray.Pdf.Fonts.Default` package project. They belong to the layout spec, which fixes the public names (see Approaches).
- Font fallback (P2), kerning and ligatures (P3), CFF outlines (P3).
- Variable-font instancing, TrueType collections, colour fonts, synthetic bold and italic, and vertical writing.

**Anti-goals.** This work fails, even if every test passes, if:

- a subset loads in our parser but fails in fontTools;
- text renders but copies out differently from its source;
- a malformed font throws any other exception, or hangs;
- `src/` gains a package reference or a public type.

## Constraints

- Every P0 constraint still holds: no runtime dependencies and no native code (NFR-01), trimming- and AOT-safe (NFR-02), `net10.0` only (NFR-03), one type per file, no partial classes, tests first.
- A loaded font is immutable and safe to share across documents and threads. The per-document state, which glyphs were used, is never shared (NFR-04).
- Fonts are untrusted input. Every offset and length is checked before it is read.
- Clean-room: the OpenType specification 1.9.1 and ISO 32000 are the only references for the font format, the subset and the PDF font objects.

## Approaches considered

**How a drawn character becomes a code in the PDF.** With Identity-H encoding, the code drawn in the content stream is the glyph ID in the subset, and the ToUnicode map gives each code its text.

| Approach | Trade-off | Outcome |
| --- | --- | --- |
| A. One code per glyph, as the design document first said | Simplest. A glyph that stands for two characters copies out as whichever was drawn first: a no-break space after a space copies as a space, and a character the font lacks copies out as nothing | Rejected |
| **B. One code per glyph and character** | Copy-out is always exact, including characters drawn as `.notdef`. A glyph used for two characters is stored twice in the subset, which costs a few bytes | **Chosen** (F-07) |
| C. As B, with a `/CIDToGIDMap` stream instead of duplicate glyphs | No duplicate glyphs, but a second mapping table to write and test, for a saving of a few bytes | Rejected |

**Where Noto Sans goes.** The design document puts the `Maviray.Pdf.Fonts.Default` package in P1.

| Approach | Trade-off | Outcome |
| --- | --- | --- |
| A. Build the package in this spec | It needs a public way to register its fonts, and that API's names are fixed in the layout spec | Rejected |
| **B. Commit the font files now, where the package will live; build the package in the layout spec** | The tests use the real fonts from the start, and the package's public method is named with the rest of the API | **Chosen** (F-12) |

## Design

Decisions carry ids F-01 to F-16. A P0 decision is cited as "P0 D-nn". Type names are working names; each plan fixes them.

### F-01 Code layout and dependencies

The font code lives in `src/Maviray.Pdf/Fonts/`, in the namespace `Maviray.Pdf.Fonts`, and every type is `internal`. Its tests go in `tests/Maviray.Pdf.Tests/Fonts/`.

The writer uses the font code to embed fonts, and the font code never uses the writer. The design document's architecture diagram gains the arrow from the PDF writer to the font engine (draft v0.4).

### F-02 What is accepted and what is refused

| Input | Result |
| --- | --- |
| sfnt version `0x00010000` or `true`, with `head`, `hhea`, `maxp`, `hmtx`, `cmap`, `loca` and `glyf` | Loaded |
| `OTTO`: CFF outlines | `NotSupportedException`: use a TrueType (`.ttf`) version of the font |
| `ttcf`: a TrueType collection | `NotSupportedException`: use a single font file |
| `wOFF`, `wOF2`: a web font | `NotSupportedException`: use the `.ttf` file |
| An `fvar` table: a variable font | `NotSupportedException`: use a static instance |
| A `COLR`, `CBDT`, `sbix` or `SVG ` table: a colour font | `NotSupportedException` |
| No Unicode `cmap` (F-04), for example only a symbol `cmap` (3, 0) | `NotSupportedException` |
| Embedding forbidden by `OS/2.fsType` (F-06) | `NotSupportedException`, naming the permission |
| Any other structural problem: a missing required table, an offset or length outside the file or its table, a bad value | `InvalidDataException`, naming the table and the problem |

No other exception type escapes the parser, the subsetter or the embedding: an out-of-range read, an overflow or an end of stream inside them is a bug. Every message names the font by its PostScript name when the `name` table could be read.

### F-03 What the parser reads

The font keeps its own copy of the file's bytes, and reads every table through bounds-checked spans over them. All values stay in font units; callers scale them.

| Table | Read |
| --- | --- |
| `head` | `unitsPerEm` (16 to 16384), the font bounding box, `macStyle`, `indexToLocFormat` (0 or 1) |
| `hhea` | ascender, descender, line gap, `numberOfHMetrics` (1 to `numGlyphs`) |
| `maxp` | `numGlyphs` (at least 1) |
| `hmtx` | The advance width of every glyph. Glyphs past `numberOfHMetrics` take the last advance |
| `cmap` | F-04 |
| `loca`, `glyf` | The byte range of each glyph. Offsets never decrease and stay inside `glyf`. An empty range is a glyph with no outline, such as a space |
| `OS/2` (optional) | `fsType`, `usWeightClass`, `fsSelection`, the typographic and Windows ascender and descender, the typographic line gap, and `sCapHeight` from version 2 |
| `post` (optional) | `italicAngle`, `isFixedPitch`, underline position and thickness |
| `name` (optional) | PostScript name (ID 6), family (ID 16, else 1), subfamily (ID 17, else 2). Windows Unicode records first, then Macintosh Roman |

Without `OS/2`, `fsType` is 0 and the weight and italic flag come from `head.macStyle`. The text spec decides which ascender and descender lines use; the parser exposes all three sets.

A glyph's outline is not parsed. The parser reads only its header (contour count and bounding box) and, for a composite glyph, its component list (F-05).

### F-04 Character to glyph (`cmap`)

The first subtable present in this order wins: (3, 10) format 12, (0, 4) format 12, (3, 1) format 4, (0, 0–3) format 4. Other formats and encodings are ignored.

- **Format 4.** Segments are searched by end code. `idDelta` is added modulo 65536, and `idRangeOffset` reads the glyph array, where an index outside the subtable is malformed. The table must end with the `0xFFFF` segment.
- **Format 12.** Groups must be sorted and must not overlap.
- A lookup returns glyph 0 for a code point the font doesn't map, for a surrogate, for a value above U+10FFFF, and for a mapping to a glyph ID of `numGlyphs` or more.

### F-05 Composite glyphs

A composite glyph (contour count below zero) lists components. The parser walks the component records, honouring every flag that changes a record's length (`ARG_1_AND_2_ARE_WORDS`, `WE_HAVE_A_SCALE`, `WE_HAVE_AN_X_AND_Y_SCALE`, `WE_HAVE_A_TWO_BY_TWO`, `MORE_COMPONENTS`), only to find each component's glyph ID and where it is stored. Transforms and instructions are copied, never interpreted.

A component ID of `numGlyphs` or more, a cycle, or nesting deeper than 16 levels is malformed. Composite glyphs are walked when a subset is built, not at load, so a broken glyph the document never uses doesn't stop the font from loading.

### F-06 Embedding permissions (`OS/2.fsType`)

When several of bits 1 to 3 are set, the least restrictive wins, as the OpenType specification says.

| `fsType` | Result |
| --- | --- |
| Bit 1 (Restricted Licence) set, bits 2 and 3 clear | Refused at load |
| Bit 9 (Bitmap embedding only) set | Refused at load: the library embeds outlines only |
| Bit 8 (No subsetting) set | Loaded. Embedded whole (F-11) |
| Anything else, including 0 (Installable) | Loaded. Embedded as a subset |

### F-07 Codes: one per glyph and character

Each document has one embedded font per loaded font. It assigns codes (CIDs) as the content draws: `Encode(glyphId, codePoint)` returns the CID for that pair, assigning the next one, from 1 upward, on first use. CID 0 is `.notdef` with no text, and `Encode` never returns it.

- The same pair always returns the same CID, in every page of the document.
- A character the font doesn't have is encoded as glyph 0 with its code point. It draws as `.notdef` and still copies out as itself.
- A font holds at most 65,535 glyphs, so CIDs run from 1 to 65,534. Encoding one more throws `InvalidOperationException`, and so does closing a document whose subset, with the components F-08 appends, would pass 65,535 glyphs.
- P3 adds a form that takes several characters, for ligatures.

The design document's "Glyph IDs are assigned on first use" is amended to match (draft v0.4).

### F-08 Subset build

The subset is built once, when the document closes. Its glyph at index *n* is the original glyph of CID *n*, so CIDs are the subset's glyph IDs and the content streams written earlier stay valid.

1. **Closure.** The components of each composite glyph, at every depth, must be in the subset. A component whose original glyph is already in the subset refers to its first subset ID. Any other component is appended after the last CID. Appended glyphs are never drawn directly.
2. **`glyf` and `loca`.** Each glyph's bytes are copied, with component glyph IDs rewritten to subset IDs, and padded with zeros to a multiple of 4 bytes. A glyph used for two characters is copied twice. `loca` uses the long format.
3. **Metrics.** `hmtx` has one full entry per glyph, so `hhea.numberOfHMetrics` and `maxp.numGlyphs` are both the subset's glyph count.
4. **`head`.** `indexToLocFormat` is 1, and `checkSumAdjustment` is recomputed. Every other field, including the dates, is copied.
5. **Other tables.** `cvt `, `fpgm`, `prep` and `gasp` are copied unchanged when present, so hinting still works. `post` is written as format 3: its header is copied, and it has no glyph names. Every other table is dropped: `cmap`, `name`, `OS/2`, `kern`, `GPOS`, `GSUB` and `GDEF` aren't needed, and `hdmx`, `LTSH` and `VDMX` would no longer match the glyph count.
6. **File.** Tables are listed in ascending tag order, start on 4-byte boundaries, are padded with zeros, and carry correct checksums. `searchRange`, `entrySelector` and `rangeShift` are computed from the table count.

The subset's bytes depend only on the font and the list of CIDs, so the same document always produces the same subset.

The design document's step 5 is amended to this table list (draft v0.4).

### F-09 PDF objects per embedded font

| Object | Entries |
| --- | --- |
| Type 0 font | `/Subtype /Type0`, `/BaseFont`, `/Encoding /Identity-H`, `/DescendantFonts [CIDFont]`, `/ToUnicode` |
| CIDFont | `/Subtype /CIDFontType2`, the same `/BaseFont`, `/CIDSystemInfo <</Registry (Adobe) /Ordering (Identity) /Supplement 0>>`, `/FontDescriptor`, `/W`, `/CIDToGIDMap /Identity` |
| Font descriptor | `/FontName` (the same name), `/Flags`, `/FontBBox`, `/ItalicAngle`, `/Ascent`, `/Descent`, `/CapHeight`, `/StemV`, `/FontFile2` |
| `FontFile2` stream | The subset, Flate-compressed like every stream (P0 D-08), with `/Length1`, its uncompressed length |
| ToUnicode stream | A CMap from each CID to its character in UTF-16BE |

**Values.** Lengths in font units are converted to the PDF's 1000-unit glyph space, ×1000 ÷ `unitsPerEm`, and written in the P0 number format (P0 D-03), so no width is rounded to a whole number.

- `/Ascent` and `/Descent` come from `hhea`, and `/FontBBox` from `head`.
- `/CapHeight` is `OS/2.sCapHeight` (version 2 or later). Otherwise it is the top of the glyph for "H" if the font maps it, and otherwise the ascent.
- `/ItalicAngle` is `post.italicAngle`, 0 without `post`.
- `/Flags` is 4 (Symbolic), because the fonts hold glyphs outside the Adobe standard Latin set, plus 1 when `post.isFixedPitch` is set, plus 64 when the font is italic (`fsSelection` bit 0, `macStyle` bit 1, or a non-zero italic angle).
- `/StemV` is `10 + 220 × (usWeightClass − 50) ÷ 900`, rounded to a whole number: 96 for regular (400) and 169 for bold (700). No table gives the real value, and a viewer uses it only to substitute a missing font, which never happens to an embedded one.
- `/W` lists every CID from 0 to the last one assigned, as one array: `[0 [w0 w1 … wn]]`. Because codes are assigned in order of use, there are no gaps to compress, so no `/DW` is needed.

**Name.** `/BaseFont` and `/FontName` are a subset tag, `+` and the PostScript name, for example `KQWBNA+NotoSans-Regular`. The tag is six capital letters taken from the SHA-256 hash of the PostScript name and the CID list, so it changes only when the subset does. Without a PostScript name, the family and subfamily are joined with a hyphen. The name keeps only the characters `0x21` to `0x7E` other than `[ ] ( ) { } < > / %`, is cut to 63 characters, and is `Font` if nothing is left.

**ToUnicode.** The CMap follows the standard form: `/CIDInit /ProcSet findresource begin`, the `Adobe`/`UCS` CIDSystemInfo, one code space range `<0000> <FFFF>`, and `bfchar` entries in CID order, at most 100 per block, as the format allows. A character above U+FFFF is written as its surrogate pair. CID 0 has no entry. The plan pins the exact bytes.

### F-10 Writer integration

- `PdfDocumentWriter.EmbedFont(TrueTypeFont)` returns the document's embedded font for it, the same instance for the same font.
- `ContentStreamBuilder.SetFont(EmbeddedFont, size)` writes `Tf`, with the same state tracking as the standard font (P0 D-09). `ShowGlyphs(ReadOnlySpan<ushort> cids)` writes the CIDs as one hex string, four uppercase digits each, followed by `Tj`.
- `ShowText` stays for the standard font, and `ShowGlyphs` refuses it. `ShowGlyphs` refuses CID 0 or a CID its font hasn't assigned, before writing anything.
- Standard and embedded fonts share one sequence of resource names (`/F1`, `/F2`, …), and each page lists only the fonts it set (P0 D-10).
- When the document closes, each embedded font builds its subset (F-08) and writes its five objects, before the page tree (P0 D-05).

### F-11 Fonts that may not be subset

A font with the No subsetting bit (F-06) is embedded whole. The `FontFile2` stream holds the original file unchanged, the name has no subset tag, and `/CIDToGIDMap` is a stream holding, for each CID from 0 to the last one assigned, its original glyph ID as two big-endian bytes. Everything else follows F-09.

### F-12 Test fonts

- **Synthetic fonts.** A test helper builds small TrueType files from a description: glyphs (simple and composite), `cmap` entries in format 4 or 12, advance widths, `unitsPerEm`, `fsType`, extra tables, and deliberate damage such as a truncated table or an offset past the end. Every parser and subsetter rule is unit-tested with these, because their expected bytes follow from the description.
- **Noto Sans.** The static hinted TTFs of Noto Sans Regular, Bold, Italic and Bold Italic, from the latest NotoSans release of the Noto project's `latin-greek-cyrillic` repository, with the project's `OFL.txt`. The P1.1 plan records the version, the file names and the SHA-256 of each file.
  - They go in `src/Maviray.Pdf.Fonts.Default/Fonts/`, where the layout spec creates the package project.
  - The tests find them by searching upward from the test output folder at run time, so no build step copies them.
  - `THIRD-PARTY-NOTICES.md` at the repository root names them, their licence (SIL Open Font License 1.1) and their source, as §6 of the licence requires.
- **Wider corpus.** DejaVu, Inter and Roboto join with the fuzzing work in P2 (NFR-10).

### F-13 Independent checks

P0 D-11 gains two tools, with the same rules: a missing tool skips the check with a message naming it, or fails it when `MAVIRAY_PDF_REQUIRE_VALIDATORS` is `true`.

- **fontTools.** It is found as `python3`, then `python`, and counts as installed only when `import fontTools` succeeds; Windows' Microsoft Store placeholder for Python doesn't. A Python script in `tests/Maviray.Pdf.Tests/Validation/`, copied to the test output and run with `-I`, does two checks:
  - **Parser.** For a font file, it prints `unitsPerEm`, `numGlyphs`, the `hhea` ascender and descender, `fsType`, and the glyph and advance width of every code point in the best `cmap`. A test compares that with our parser, for every Noto Sans style.
  - **Subset.** For an original font, a subset and the list of original glyph IDs per subset glyph, it loads the subset with checksum errors raised, decompiles every table, and checks each subset glyph against its original: the same contours, points and flags (for a composite, the same components after mapping and the same transforms), and the same advance width. It also checks `numGlyphs` and `numberOfHMetrics`. Our own test checks `checkSumAdjustment`.
- **pdffonts** (poppler-utils, already installed). Every embedded font is reported with type `CID TrueType`, `emb` yes, `sub` yes (no for F-11), `uni` yes, and its tagged name.

### F-14 CI and contributors

- `build.yml` and `publish-nuget.yml` add `python3-fonttools` to the `apt-get install` line, and print its version with the other tools.
- `CONTRIBUTING.md`, under "Running the tests", adds fontTools and how to install it (`pip install fonttools`, or the system package).

### F-15 Fonts gate

A test writes one A4 page with six lines in each of the four Noto Sans styles:

| Language | Line |
| --- | --- |
| German | `Grüße aus Köln: Äpfel, Öl und Übermaß` |
| Turkish | `Iğdır'da ılık bir gün; İstanbul, Şişli` |
| Azerbaijani | `Əli Gəncədə yaşayır` |
| Russian | `Счёт № 42 от 20 ноября` |
| Ukrainian | `Ґанок, їжак і євро` |
| Greek | `Τιμολόγιο για την Αθήνα` |

It passes when:

- `qpdf --check` reports no errors;
- `pdffonts` reports four fonts, each embedded, subset and Unicode-mapped (F-13);
- `pdftotext` returns all 24 lines exactly;
- `pdftoppm` renders ink inside each line's box and nothing outside the lines;
- writing the file twice, with the same file identifier (P0 D-07), gives identical bytes.

The viewer checklist (`docs/qa/checklists/viewer-check.md`) gains this file, with its description and a clipboard check. Its viewer check by hand joins the 0.1.0 release check. The maintainer can run it as soon as P1.4 merges, but it isn't a merge condition.

### F-16 Documentation and delivery

As P0 D-14: one GitHub issue per work item in the milestone `v0.1.0`, a plan and an execution log per item, one branch `feature/<n>-<slug>` and one pull request into `develop` per item, commit subjects `#<n> <summary>`, and issues closed by hand after the merge. This spec and the design-document amendments (draft v0.4) ship in one docs pull request.

## Work items

They run in order, because each builds on the one before.

| Item | Scope | Tests pin | Done when |
| --- | --- | --- | --- |
| **P1.1** TrueType parser | F-01 to F-06, F-12, F-13 (parser check), F-14 | Each refusal and its exception type; each `cmap` format and edge (`idRangeOffset`, the `0xFFFF` segment, unsorted groups, IDs past `numGlyphs`); advances past `numberOfHMetrics`; `fsType` precedence; damaged files of every kind throw `InvalidDataException` | Our parser agrees with fontTools on all four Noto Sans styles in CI |
| **P1.2** Subsetter | F-08 | Closure through nested composites; component IDs rewritten; a glyph used twice copied twice; tables kept and dropped; checksums and `checkSumAdjustment`; a cycle and too-deep nesting refused; the same input gives the same bytes | Subsets of all four Noto Sans styles pass the fontTools subset check in CI |
| **P1.3** Embedding | F-07, F-09 to F-11, F-13 (pdffonts) | `Encode` order and reuse; `.notdef` characters; the CID limit; the bytes of each PDF object, `/W` and the ToUnicode CMap; the name rules; `ShowGlyphs` refusals; the whole-font path | A one-line Noto Sans PDF passes qpdf and pdffonts, and `pdftotext` returns its line exactly |
| **P1.4** Fonts gate | F-15 | The gate test with every check | The gate test passes in CI, and the checklist describes its file |

## Left to the builder

- Type and member names beyond those fixed above. Each plan settles them.
- The shape of the synthetic-font helper, and how the Python script receives its arguments.
- Caching of `cmap` lookups, if profiling ever asks for it.
- The exact Noto Sans release, checked when the files are fetched.
