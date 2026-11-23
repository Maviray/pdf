# P1 Text: design

**Status:** draft, 2026-11-23, awaiting approval. Release 0.1.0. The second of three specs for phase P1 of the [design document](../../../design/Maviray.Pdf-Design-Document.md) (draft v0.5): fonts, then text, then layout with the fluent API.

## Intent

P1 needs paragraphs, not single lines. This spec covers the text engine, which turns a paragraph of styled spans into lines of positioned glyphs at a given width. It runs the design document's pipeline:
- normalize to NFC;
- keep grapheme clusters whole;
- map characters to glyphs;
- find line-break opportunities by Unicode's rules (UAX #14);
- fill lines greedily;
- place each line by its metrics, alignment and letter spacing, with underline and strikethrough.

The layout spec's Text element measures, splits across pages and draws through this engine.

Two findings shape the spec:
- **.NET's NFC depends on the platform.** `string.Normalize` uses ICU. In invariant-globalization mode, which containers and Native AOT builds commonly use, it returns decomposed text unchanged, and `IsNormalized` still reports true. A probe on .NET 10 showed exactly this.
- **.NET has no line-breaking data.**

The library therefore implements both itself, from Unicode Character Database files.

**Gate:** a PDF of wrapped paragraphs passes these checks (T-15):
- German, Turkish, Russian and Greek text, in three styles;
- left, centred and right alignment;
- a soft hyphen, a word broken because it can't fit, letter spacing, underline and strikethrough;
- every line breaks where Unicode allows, and holds as much as fits;
- the page passes qpdf;
- `pdftotext` returns every line exactly.

## Goals, non-goals, anti-goals

**Goals**

1. NFC normalization that gives the same result on every platform and in every globalization mode, and passes every test in Unicode's `NormalizationTest.txt`.
2. Line-break opportunities by the default rules of UAX #14, passing every test in Unicode's `LineBreakTest.txt`.
3. Lay out a paragraph of styled spans at any width:
   - greedy filling, mandatory breaks and soft hyphens;
   - words too wide for the line, broken between grapheme clusters;
   - line heights and baselines from the font's metrics;
   - left, centred and right alignment;
   - letter spacing, underline and strikethrough.
4. Give the writer what it needs to draw those lines exactly as laid out, so they render and copy out correctly.

**Non-goals for this spec**

- **Any public type.** The public text-style API, matching fonts by family, weight and style, `FontRegistry`, the Text element and splitting text across pages all belong to the layout spec, and so do page-number spans and caching per width.
- **Later phases:** justification, font fallback and widow and orphan control (P2); kerning, ligatures and hyphenation (P3); bidirectional text and complex scripts (P5).
- **Not planned:** vertical text, tab stops, and line-breaking rules tailored to a language.
- **A grapheme-cluster implementation of our own.** .NET's is used (T-05).

**Anti-goals.** This work fails, even if every test passes, if:

- the same paragraph lays out or copies out differently on another platform or globalization mode;
- a character the font has draws as `.notdef`, or an invisible character draws as a box;
- a measured line width, given back as the available width, lays out different lines, because Row's auto widths depend on it;
- text copies out as anything other than its NFC form, apart from the invisible characters T-07 lists;
- `src/` gains a package reference or a public type, reads a file at run time, or calls an API whose result depends on ICU or the culture.

## Constraints

- Every P0 and P1 fonts constraint still holds:
  - no runtime dependencies and no native code (NFR-01);
  - trimming- and AOT-safe (NFR-02);
  - `net10.0` only (NFR-03);
  - one type per file, no partial classes, and tests first.
- The text engine uses the font engine and nothing else, as the architecture diagram shows. It never uses the writer.
- Prepared paragraphs and layouts are immutable, so they can be shared across threads. They hold no document state: CIDs are assigned only when a page draws them (F-07).
- The Unicode data is the Unicode Character Database, version 16.0.0, from unicode.org, under the Unicode License v3. It is the version of .NET 10's own character data:
  - U+1C89, new in 16.0, is assigned in .NET 10;
  - U+20C1, new in 17.0, isn't.
- Clean-room. The references for the text engine are:
  - UAX #14 (line breaking), UAX #15 (normalization) and UAX #29 (grapheme clusters), in their versions for 16.0;
  - the Unicode Standard, chapter 3;
  - the OpenType specification 1.9.1.

## Approaches considered

**NFC normalization**

| Approach | Trade-off | Outcome |
| --- | --- | --- |
| A. `string.Normalize` | No code to write. But it returns decomposed text unchanged in invariant mode, and with ICU its result follows the ICU version installed. Output would differ between platforms | Rejected |
| **B. Our own UAX #15 NFC from the UCD** | An algorithm to maintain, and generated tables of a few tens of kilobytes. The result is the same everywhere, and Unicode's conformance file tests it | **Chosen** (T-04) |
| C. No normalization: draw decomposed text as base and mark glyphs | Nothing to build. But v1 has no mark positioning (GPOS), so accents sit wherever the font's default puts them. The design document asks for NFC | Rejected |

**Line breaking**

| Approach | Trade-off | Outcome |
| --- | --- | --- |
| A. Break at spaces and after hyphens only | Small. Wrong around punctuation, quotes, numbers, no-break spaces, slashes and dashes. The next language would need a rewrite | Rejected |
| **B. UAX #14's default rules, run rule by rule, tested by `LineBreakTest.txt`** | The largest piece of this spec. But it is correct for every script's break classes, and conformance is a pass or fail against Unicode's own file | **Chosen** (T-06) |
| C. UAX #14's older pair table | Faster to write. But since Unicode 15.1 several rules look further back than one pair (quotation marks, numbers, Brahmic scripts), so it can't pass the conformance file | Rejected |

**Where the Unicode data lives**

| Approach | Trade-off | Outcome |
| --- | --- | --- |
| A. Tables written by hand | No download. But it's our approximation of Unicode, and nothing proves it right | Rejected |
| **B. C# tables generated from the UCD files, which are committed in the repository** | The tables compile into the assembly and nothing is read at run time. A test regenerates them and fails if the committed ones differ | **Chosen** (T-02) |
| C. Read the UCD files at run time | No generated code. But it means file access and a start-up cost, and it can't run in WebAssembly | Rejected |

**Grapheme clusters:** .NET's `StringInfo` is used rather than a UAX #29 implementation of our own. It is fully managed and works in invariant mode, which the same probe showed. Its one gap is that it lacks rule GB9c (Indic conjuncts, Unicode 15.1), which doesn't affect v1's scripts. The probe showed the gap: "क्ष" is two clusters, where 15.1 makes it one.

## Design

Decisions carry ids T-01 to T-16. A fonts decision is cited as "F-nn", and a P0 decision as "P0 D-nn". Type names are working names; each plan fixes them.

### T-01 Code layout and dependencies

- **Production code** lives in `src/Maviray.Pdf/Text/`, in the namespace `Maviray.Pdf.Text`, and every type is `internal`.
- **Generated Unicode tables** live in `src/Maviray.Pdf/Text/Unicode/`.
- **Tests** go in `tests/Maviray.Pdf.Tests/Text/`.
- **No ICU or culture.** A test fails if a file in `src/` calls `Normalize` or `IsNormalized`, or if a file in `src/Maviray.Pdf/Text/` names `CultureInfo`, `CompareInfo` or `TextInfo`.
- **Both globalization modes.** CI runs the text tests a second time with `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT` set to `true`.

### T-02 Unicode data

**Files.** These UCD 16.0.0 files are fetched once from `https://www.unicode.org/Public/16.0.0/ucd/` and committed unchanged:

| File | Used for |
| --- | --- |
| `UnicodeData.txt` | General category, canonical combining class, canonical decompositions |
| `CompositionExclusions.txt` | Characters excluded from composition |
| `DerivedCoreProperties.txt` | `Default_Ignorable_Code_Point` (T-07) |
| `LineBreak.txt` | Line-break classes |
| `EastAsianWidth.txt` | The East Asian width that several UAX #14 rules test |
| `emoji/emoji-data.txt` | `Extended_Pictographic`, for rule LB30b |
| `NormalizationTest.txt` | NFC conformance (T-04) |
| `auxiliary/LineBreakTest.txt` | Line-breaking conformance (T-06) |

Approving this spec approves that download, together with `https://www.unicode.org/license.txt`. Nothing else is fetched.

**Storage**
- **Where:** the files go in `tests/Maviray.Pdf.Tests/Text/Unicode/Data/16.0.0/`.
- **Compression:** each is gzip-compressed (`<name>.txt.gz`), to roughly a tenth of its size, because the test files alone run to several megabytes.
- **Licence:** `license.txt` sits beside them, uncompressed.
- **Checksums:** a test checks the SHA-256 of each decompressed file and of the licence against the values the P1.5 plan records.
- **Line endings:** `.gitattributes` marks `*.gz` as binary, and keeps `license.txt` with LF line endings.

**Generated tables**
- **Where they come from:** a generator in the test project reads the compressed files and writes C# files into `src/Maviray.Pdf/Text/Unicode/`. Each file opens with a comment naming its source, version and generator, the Unicode copyright and permission notice, and "Do not edit".
- **Form:** each table is a `static ReadOnlySpan<T>` over constant data, so nothing is allocated or parsed at start-up.
- **Lookups:** they take a code point and never fail. An unassigned code point gets the defaults the UCD files state in their `@missing` lines.
- **Staleness check:** a test regenerates every table in memory and fails if a committed file differs. With `MAVIRAY_PDF_WRITE_UNICODE_TABLES` set to `true`, it writes the files instead. `.gitattributes` keeps the generated files with LF line endings, so the comparison is exact on every platform. `CONTRIBUTING.md` says how to regenerate them.

**Notices.**
- `THIRD-PARTY-NOTICES.md` gains a Unicode section: the files, the version, the source, and the full Unicode License v3 text. It says that the tables in `Maviray.Pdf.dll` are derived from them.
- Every package ships `THIRD-PARTY-NOTICES.md` at its root, beside `LICENSE`, as the Unicode License requires.

### T-03 Input text

Each span's text is cleaned before anything else:
1. An unpaired surrogate becomes U+FFFD.
2. The text is then normalized to NFC (T-04), span by span. A span boundary can therefore separate a base letter from a following combining mark, and T-05 still keeps them in one cluster.
3. The paragraph's text is the spans' normalized texts, joined. Every later stage works on it, and every range the engine reports is a UTF-16 range in it.

### T-04 NFC normalization

The algorithm is the Unicode Standard's, chapter 3.11, with Hangul syllables composed and decomposed arithmetically (chapter 3.12):
1. Full canonical decomposition. Mappings are expanded at generation time, so a lookup gives the final sequence.
2. Canonical ordering: a stable sort of each run of non-starters by combining class.
3. Canonical composition. A character joins the last starter when it isn't blocked and the pair has a primary composite that isn't excluded. The full composition exclusions are derived at generation time from three sources:
   - `CompositionExclusions.txt`;
   - singletons;
   - decompositions that start with a non-starter, or that belong to one.

**Fast path.** A quick check (`NFC_Quick_Check`, also derived from the data) finds the first code point that might change. It then works from the last starter before it. A string that is already NFC is returned as the same instance, with nothing allocated.

**Conformance:**
- every line of `NormalizationTest.txt` passes all of its NFC conditions, c2 = NFC(c1) = NFC(c2) = NFC(c3) and c4 = NFC(c4) = NFC(c5), in all four parts;
- NFC leaves every other code point, surrogates aside, unchanged.

**Input size.** The implementation doesn't recurse, so a long run of combining marks costs a sort and nothing worse.

### T-05 Grapheme clusters

Clusters are found with `StringInfo.GetNextTextElementLength` over the paragraph's text, so they cross span boundaries. A line never breaks inside a cluster: a break opportunity from T-06 that falls inside one is ignored. Tests pin the cases v1 meets:
- a base letter with one or more combining marks, including a Cyrillic vowel with U+0301;
- CR LF;
- a regional-indicator pair;
- an emoji ZWJ sequence.

### T-06 Line-break opportunities

UAX #14's default algorithm for 16.0, rules LB1 to LB31, applied rule by rule to the paragraph's code points. Between two code points, each position is one of three: no break, break allowed, or break required. No language tailors it. LB1 resolves the classes UAX #14 leaves to the implementation:
- AI, SG and XX become AL;
- SA becomes CM for general categories Mn and Mc, and AL otherwise;
- CJ becomes NS.

The generated table gives each code point the properties the rules test:
- its resolved class;
- whether a QU character is Pi or Pf (LB15a, LB15b);
- whether its East Asian width is F, W or H;
- whether it is `Extended_Pictographic` and unassigned (LB30b).

**Conformance:** every test in `LineBreakTest.txt` passes, under any assumption the file's header states.

**Break characters.** A required break comes after BK (U+000B, U+000C, U+2028, U+2029), CR, LF and NEL (U+0085). CR LF is one break. These characters end their line and are never drawn.

### T-07 Glyphs for characters

A span's style gives one font and one size (T-14). Each code point gets a glyph from the font's `cmap` and an advance from `hmtx`, scaled by size ÷ `unitsPerEm`. These rules come first, in order:

| Code point | Drawn as | Copies out as |
| --- | --- | --- |
| A break character (T-06) | Nothing | Line end |
| U+0009 tab | The glyph and advance of U+0020, or nothing if the font lacks U+0020. v1 has no tab stops | U+0009 |
| U+00AD soft hyphen | Nothing, unless its line breaks right after it and text follows. Then a hyphen: the glyph of U+2010, or else U+002D, with its advance. If the font has neither, nothing | U+00AD at the line end, otherwise nothing |
| Mapped by the font | Its glyph | Itself |
| Not mapped, and `Default_Ignorable_Code_Point` (such as U+200B, U+200D, U+2060, U+FEFF and variation selectors) or general category Cc | Nothing, with no advance and no report | Nothing |
| Not mapped, a space separator (Zs, such as U+00A0 or U+202F), and the font maps U+0020 | The glyph and advance of U+0020 | Itself |
| Not mapped, with a canonical decomposition whose every code point the font maps | Those glyphs, each with its own code point | Its decomposition |
| Anything else | `.notdef` (glyph 0) with glyph 0's advance, and reported as missing | Itself (F-07) |

The prepared paragraph lists every missing code point with the span it came from. The layout spec turns these into the design document's diagnostic event, and font fallback arrives in P2.

### T-08 Line metrics and line height

**Metric set.** A font's ascent A, descent D and line gap G come from `OS/2`'s typographic values when `OS/2` exists and `fsSelection` bit 7 (`USE_TYPO_METRICS`) is set, and from `hhea` otherwise. The PDF's `/Ascent` and `/Descent` keep coming from `hhea` (F-09). Noto Sans sets bit 7, and both sets are 1069, −293 and 0 there.
- A negative line gap counts as 0.
- If A − D isn't positive, A is 0.8 em and D is −0.2 em.

**Line height.** A style's line height is a multiple of its size. When it isn't set, it is the font's normal line height, (A − D + G) ÷ `unitsPerEm`: 1.362 for Noto Sans.
- For a span at size s with line height m, the leading is L = m·s − (A − D)·s ÷ `unitsPerEm`, which may be negative.
- The span sits A·s ÷ `unitsPerEm` + L/2 above the baseline and −D·s ÷ `unitsPerEm` + L/2 below it.
- The spans considered are every span with at least one code point on the line, including hanging spaces and the break character.
- A line's height above its baseline is the largest "above" among those spans, and its depth is the largest "below". Its height is their sum, and its baseline lies "above" points below its top.
- An empty line, one that holds only a break character, takes the span of that character.

The paragraph's height is the sum of its lines' heights. There is no spacing between lines beyond the line heights, and no spacing between paragraphs.

### T-09 Filling lines

**Width.** The available width is positive and finite, or positive infinity, which the layout spec uses to measure an auto width. Anything else throws `ArgumentOutOfRangeException`. All values are points, in `double`.

**Lines.** The text between two required breaks is filled on its own:
- A segment is the text from one allowed break to the next. Its spaces belong to its end, because UAX #14 breaks after spaces.
- A segment goes onto the line when the line, ended right after that segment, fits. A segment that ends at a soft hyphen is therefore tested with the hyphen, even if a later segment then joins the line.
- When a segment doesn't fit, the line ends before it, and the segment starts the next line.

**Fit.** A line fits when its width is at most the available width plus 10⁻⁶ point. That tolerance absorbs floating-point noise in widths computed by the layout. A line's width is the sum of its drawn advances and the letter spacing between them (T-10). It counts neither of these:
- trailing U+0020 and U+0009, which hang: they aren't measured, drawn or decorated;
- the break character.

If the line ends at a soft hyphen, the hyphen's advance counts.

**Words too wide for a line.** A segment that doesn't fit on an empty line is broken between grapheme clusters. The line takes the most clusters that fit, and at least one. The rest continues on the next line, where normal breaking resumes. No hyphen is drawn at such a break.

**Overflow.** A line can be wider than the available width plus the tolerance only when it holds a single cluster. The engine reports that line's true width, and the layout spec decides what an overflow means (FR-09).

**Required breaks and empty text.**
- Every required break ends a line.
- Text that ends with a required break gets no empty line after it, and empty text gives no lines.
- So `"a\n\nb"` gives three lines, `"\n"` one empty line, and `""` none.

**The measured width round-trips.** Filling a paragraph at the widest line width of an earlier fill gives the same lines, whenever no line of the earlier fill was wider than its available width. A fill at infinity always meets that condition, and it is how auto widths are measured.

### T-10 Alignment and letter spacing

**Alignment.** Left, centre or right, per paragraph. A line's offset from the left edge is 0, (W − w) ÷ 2 or W − w, where W is the available width and w the line's width.
- A line wider than W, and every line at infinite width, gets offset 0, as in CSS.
- Justification arrives in P2.
- Lines always run left to right, in the text's order. v1 doesn't reorder bidirectional text.

**Letter spacing.** A style's letter spacing is a length in points, and may be negative.
- It is added after each drawn grapheme cluster, using that cluster's own style, except after the last drawn cluster of a line.
- Hanging spaces get none.
- A hyphen drawn at a soft hyphen counts as a cluster.
- Combining marks inside a cluster get no spacing, so they stay on their base letter.

### T-11 Underline and strikethrough

Each is a filled rectangle in the span's colour, which the layout spec supplies. There is one rectangle per run of consecutive clusters of one decorated span on a line.
- **Extent:** it runs from the run's first cluster to the end of its last cluster's advance. That includes the inner spaces and the letter spacing between its clusters, but not hanging spaces.
- **Position and thickness:** each comes from the span's own font and size.

| Decoration | Top edge above the baseline | Thickness | When the font gives no value or a thickness of 0 |
| --- | --- | --- | --- |
| Underline | `post.underlinePosition` (negative: below) | `post.underlineThickness` | Top at −0.1 em, thickness 0.05 em |
| Strikethrough | `OS/2.yStrikeoutPosition` | `OS/2.yStrikeoutSize` | Top at 0.3 em, with the underline's thickness |

Both values are the top edge of the stroke, as the OpenType specification defines them, so the rectangle extends downward from it by the thickness. For Noto Sans Regular at 10 pt, the underline spans 1 to 1.5 pt below the baseline, and the strikethrough 3.22 to 2.72 pt above it.

### T-12 Font parser additions

`TrueTypeFont` exposes three more values. The P1.1 fontTools parser check compares them too.
- `OS/2.yStrikeoutPosition` and `yStrikeoutSize`, at offsets 28 and 26, present in every `OS/2` version;
- whether `fsSelection` bit 7 is set.

### T-13 Positioned glyphs in the content stream

`ContentStreamBuilder` gains a form of `ShowGlyphs` that takes, beside the CIDs, an extra advance in points after each one. It writes one `TJ` array with the same checks as F-10's `Tj`:
- each run of CIDs with no extra advance is one hex string;
- each extra advance d is written between them as −d × 1000 ÷ the font size, in the P0 number format (P0 D-03);
- an extra advance of 0 writes nothing, after the last CID too, and one that isn't finite is refused before anything is written.

For example, `[<00010002> -50 <0003>] TJ`.

This is how letter spacing is drawn. It is also how justification will place words in P2, because `Tw` doesn't apply to two-byte codes. The text engine's advances are the same `hmtx` values the font's `/W` array holds (F-09), so a viewer advances each glyph exactly as the layout did.

### T-14 The text engine's model

**Style.** A span's style holds the values that decide geometry:
- the font, a loaded `TrueTypeFont`;
- the size, positive and finite;
- the line height, a positive, finite multiple of the size, or not set (T-08);
- the letter spacing, finite;
- whether it is underlined, and whether it is struck through.

Colour isn't part of it: a layout refers to spans by index, and the drawing side looks up their colour. A paragraph is a list of spans and an alignment. Invalid values throw `ArgumentException` or `ArgumentOutOfRangeException`, naming the value.

**Two steps**

1. **Prepare** a paragraph once. This step doesn't depend on the width. It runs T-03 to T-07 and keeps the results:
   - the normalized text;
   - the span ranges;
   - the clusters;
   - the glyphs and their advances;
   - the break opportunities;
   - the missing code points.
2. **Fill** the prepared paragraph at a width (T-08 to T-11). The result is a layout:
   - the widest line width, and the total height;
   - its lines. Each line holds its range in the text, its width, its offset, its height and its baseline.
   - each line's glyph runs, one per span segment. A run holds the span's index, its x position and its glyphs. Each glyph holds its glyph ID, its code point, its x position and the extra advance after it.
   - each line's decorations: the kind, the span's index, x, width, top edge and thickness.

**Coordinates.** Positions are in points from the paragraph's left edge and each line's top, with y growing downward, like the layout spec's canvas.

**Performance.** Fill can run many times per paragraph, once for each width the layout asks for. It therefore avoids LINQ and allocations per glyph, and never repeats preparation.

### T-15 Text gate

A test writes one A4 page of paragraphs in Noto Sans Regular, Bold and Italic at 10 pt. A test helper draws the layouts with `ShowGlyphs` and its `TJ` form, and fills the decorations as rectangles. The paragraphs are stacked in one column, so the reading order is unambiguous. The P1.8 plan fixes their exact strings.

| Paragraph | Exercises |
| --- | --- |
| German prose, about 80 words, left-aligned at 220 pt | Greedy filling. A soft hyphen in a compound placed where the line breaks. `z.` U+202F `B.` and `20` U+00A0 `€`, which must not break. `E-Mail-Adresse`, `1.234,56 €` |
| The same prose, centred at 160 pt and right-aligned at 160 pt | Alignment |
| Mixed spans | Bold and italic spans, a 14 pt span (line heights differ), underline, strikethrough, 0.5 pt letter spacing, a `\n`, and Turkish, Russian and Greek sentences |
| `DE89370400440532013000` at 70 pt | A word broken because it can't fit |
| `Ko` U+0308 `ln, Mu` U+0308 `nchen` | Decomposed input, which draws and copies out as NFC |

It passes when:

- **every line is a correct greedy line**, by a check that doesn't use the filler. The check computes each line's width from the font's advances and confirms three things:
  - the line fits;
  - adding the next segment wouldn't fit;
  - every break is a UAX #14 opportunity or, for the long word, a cluster boundary;
- the lines' text equals the expected lines written out in the test;
- `pdftotext` (poppler) returns those lines, in order. A line that ends at the soft hyphen ends with U+00AD;
- `pdftoppm` renders ink inside each line's box and nowhere else. Each box is the line's offset and width, and its top and height, with a small margin;
- `qpdf --check` reports no errors, and `pdffonts` reports three embedded, subset, Unicode-mapped fonts;
- writing the file twice gives identical bytes.

The viewer checklist (`docs/qa/checklists/viewer-check.md`) gains this file, with its description and a clipboard check. Its check by hand joins the 0.1.0 release check, like the fonts file's, and isn't a merge condition.

### T-16 Documentation and delivery

As F-16:
- one GitHub issue per work item in the milestone `v0.1.0`;
- a plan and an execution log per item;
- one branch `feature/<n>-<slug>` and one pull request into `develop` per item;
- commit subjects `#<n> <summary>`;
- issues closed by hand after the merge.

This spec and the design-document amendments (draft v0.5) ship in one docs pull request. The amendments are: NFC by our own implementation and why, the metric set, words broken between clusters, the Unicode notice in the packages, and the conformance tests in the testing table.

## Work items

They run in order, because each builds on the one before.

| Item | Scope | Tests pin | Done when |
| --- | --- | --- | --- |
| **P1.5** Unicode data and NFC | T-01 to T-04 | The data files' checksums; the tables are current; the fast path returns the same instance; Hangul both ways; the composition exclusions; unpaired surrogates; the ICU and culture ban | Every NFC condition of `NormalizationTest.txt` passes, and `THIRD-PARTY-NOTICES.md` carries the Unicode licence |
| **P1.6** Line breaking | T-05, T-06 | The cluster cases of T-05; the LB1 resolutions; the break characters; CR LF | Every test in `LineBreakTest.txt` passes |
| **P1.7** Text layout | T-07 to T-14 | Each glyph rule of T-07, and both metric sets. Filling: the fit tolerance, hanging spaces, the soft hyphen, the long word, required breaks and empty text. Every alignment, including overflow; letter spacing, also around marks; each decoration with and without font values; the `TJ` bytes; invalid styles and widths. The width round trip, over many random paragraphs and widths, with the seed logged | A paragraph's layout draws on a PDF page that passes qpdf |
| **P1.8** Text gate | T-15 | The gate test with every check | The gate test passes in CI, and the checklist describes its file |

## Left to the builder

- Type and member names beyond those fixed above. Each plan settles them.
- The tables' shape: two-stage arrays, sorted ranges or another compact form, as long as a lookup can't fail and nothing allocates.
- How the line breaker keeps its state between rules, and how filling tracks the last break that fits.
- The exact gate strings, widths and positions, within T-15.
