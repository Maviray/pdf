# P1.3 Embedded TrueType fonts: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A document can embed a TrueType font, draw text in it as CIDs, and close with the five PDF objects of F-09. The subset is embedded, or the whole font when its licence says so. Every character prints, copies out and searches correctly: qpdf, pdffonts and pdftotext confirm a line of Noto Sans.

**Architecture:**
- A small abstract `PdfFont` lets `FontResources`, the content-stream builder and the page resources treat the standard font and an embedded font alike.
- `EmbeddedFont` belongs to one document. It hands out one CID per (glyph, character) pair, and when the document closes it writes its objects: it calls P1.2's `FontSubsetter`, or embeds the whole file.
- The values that go into those objects are built by three small pure types, each tested byte for byte:
  - `ToUnicodeCMap`;
  - `EmbeddedFontName` (the subset tag and the name rules);
  - `FontDescriptor`, with `CidWidths`.
- pdffonts joins the validators.

**Tech Stack:** .NET 10, C#, xUnit v3; qpdf and poppler-utils (pdffonts, pdftotext) in CI, already installed.

**Spec:** [P1 Fonts](../specs/2026-11-20-p1-fonts-design.md), decisions F-07, F-09, F-10, F-11 and F-13 (pdffonts), with P0 D-03 (numbers), D-05 (write order), D-08 (streams) and D-10 (resource names). Work item [#22](https://github.com/Maviray/pdf/issues/22). The branch `feature/22-embedded-truetype-fonts` is stacked on `feature/21-truetype-subsetter` (PR #25).

## Global Constraints

- Every P1.1 and P1.2 constraint holds:
  - everything is `internal`, one type per file;
  - no package references;
  - C# with backslashes is written with the file tools;
  - expected values come from the spec, ISO 32000, the OpenType specification, a tool we didn't write, or a throwaway computation while planning, never from the code under test.
- New writer types live in `Maviray.Pdf.Writer`, under `src/Maviray.Pdf/Writer/`. The writer may use `Maviray.Pdf.Fonts`, and the fonts layer never uses the writer (F-01).
- Errors:
  - a caller's wrong argument throws `ArgumentException` or `ArgumentOutOfRangeException`;
  - a wrong operator order or a reached limit throws `InvalidOperationException`;
  - a damaged font found while closing throws `InvalidDataException` from P1.2, unchanged.
  - A method that throws writes nothing.
- An embedded font belongs to one document, and its CIDs are never shared (NFR-04).
- Numbers in font objects are converted from font units as `value × 1000 ÷ unitsPerEm` and written as `PdfReal` in the P0 number format (D-03: at most four decimals, midpoints away from zero).
- Tests that read object text write with `CompressionLevel.NoCompression`. Tests that run qpdf, pdffonts or pdftotext save with `GeneratedFiles.Save`.
- Each task ends with one commit, subject `#22 <summary>`. The records are the last commit.

## Review Focus

1. **Characters the font lacks, and characters above U+FFFF.** A character the font lacks is drawn as `.notdef` and still copies out as itself. A character above U+FFFF is written in ToUnicode as its surrogate pair. Pinned in Task 1, and in Task 5 by pdftotext on Noto Sans with a CJK character.
2. **Large and full fonts.** Two limits apply:
   - ToUnicode splits into `bfchar` blocks of at most 100 entries;
   - after 65,534 CIDs, `Encode` throws.

   Closing a document whose subset, components included, would pass 65,535 glyphs throws `InvalidOperationException`. Pinned in Tasks 1, 3 and 4.
3. **Fonts across documents and after closing.** An embedded font set in another document's content is refused, and `Encode` on a font already written is refused (NFR-04). Pinned in Tasks 3 and 4.
4. **Names a PDF can't hold.** A PostScript name with spaces, delimiters, `%`, non-ASCII characters or more than 63 characters is cleaned up, and an empty result becomes `Font`. Without a PostScript name, the family and subfamily are used. Pinned in Task 2. This closes P1.1's ruling that left cleaning the name to P1.3.
5. **`unitsPerEm` other than 1000.** Widths, the bounding box and the metrics are scaled and keep four decimals; nothing is rounded to a whole number. Pinned in Task 2.

---

### Task 1: ToUnicode CMap

**Files:**
- Create: `src/Maviray.Pdf/Writer/ToUnicodeCMap.cs`
- Test: `tests/Maviray.Pdf.Tests/Writer/ToUnicodeCMapTests.cs`

**Interfaces:**
- Produces `internal static class ToUnicodeCMap`: `static byte[] Build(IReadOnlyList<int> codePoints)`. `codePoints[cid]` is the character of each CID. Index 0 (`.notdef`) is ignored and gets no entry. The result is ASCII, and every line ends with LF.
- The exact text, for CIDs 1 to 3 holding `A`, `B` and U+1F600:

  ```text
  /CIDInit /ProcSet findresource begin
  12 dict begin
  begincmap
  /CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def
  /CMapName /Adobe-Identity-UCS def
  /CMapType 2 def
  1 begincodespacerange
  <0000> <FFFF>
  endcodespacerange
  3 beginbfchar
  <0001> <0041>
  <0002> <0042>
  <0003> <D83DDE00>
  endbfchar
  endcmap
  CMapName currentdict /CMapResource defineresource pop
  end
  end
  ```

  - CIDs are 4 uppercase hex digits.
  - A character above U+FFFF is its UTF-16 surrogate pair, as 8 digits.
  - A block holds at most 100 entries, and a new `n beginbfchar` follows.
  - With no CID past 0, there is no `beginbfchar` block at all.

- [ ] **Step 1: Failing tests:**
  - `WritesTheStandardFormWithASurrogatePair`: the text above, byte for byte.
  - `SplitsIntoBlocksOf100`. For 250 CIDs, each holding `0x4E00 + cid`:
    - the block headers are `100 beginbfchar`, `100 beginbfchar` and `50 beginbfchar`, in that order;
    - the entries run from `<0001> <4E01>` to `<00FA> <4EFA>`.
  - `WithOnlyNotdefThereAreNoEntries`: for `[0]`, the text has no `beginbfchar`, and it still ends `end\nend\n`.
- [ ] **Step 2:** Verify they fail. Expected: CS0103 (`ToUnicodeCMap` doesn't exist).
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Verify they pass, and run the whole suite. Commit `#22 write ToUnicode CMaps`.

### Task 2: Name, widths and font descriptor

**Files:**
- Create: `src/Maviray.Pdf/Writer/EmbeddedFontName.cs`, `CidWidths.cs`, `FontDescriptor.cs`
- Modify: `src/Maviray.Pdf/Writer/PdfName.cs`. Add the names the font objects use as static properties, like the existing ones:
  - `Type0`, `IdentityH` (`Identity-H`), `DescendantFonts`, `ToUnicode`, `CIDFontType2`, `CIDSystemInfo`;
  - `Registry`, `Ordering`, `Supplement`, `FontDescriptor`, `W`, `CIDToGIDMap`, `Identity`;
  - `FontName`, `Flags`, `FontBBox`, `ItalicAngle`, `Ascent`, `Descent`, `CapHeight`, `StemV`, `FontFile2`, `Length1`.
- Test: `tests/Maviray.Pdf.Tests/Writer/EmbeddedFontNameTests.cs`, `FontDescriptorTests.cs` (which also covers `CidWidths`)

**Interfaces:**
- Consumes: `TrueTypeFont` (P1.1) and `TestFontBuilder` (tests).
- Produces:
  - `internal static class EmbeddedFontName`:
    - `static string BaseName(TrueTypeFont font)` is the cleaned name without a tag. Cleaning:
      1. Start from the PostScript name. Without one, use the family and the subfamily joined with `-`, or the family alone.
      2. Keep only the characters `0x21` to `0x7E` other than `[ ] ( ) { } < > / %`.
      3. Cut the result to 63 characters.
      4. Use `Font` if nothing is left.
    - `static string Tagged(TrueTypeFont font, IReadOnlyList<ushort> glyphs)` is `<TAG>+<BaseName>`. `glyphs[cid]` is the original glyph of each CID, and index 0 is left out of the hash.
      - The hash is SHA-256 over the ASCII bytes of the base name, followed by each glyph from CID 1 on as 2 big-endian bytes.
      - Tag letter *i* is `'A' + hash[i] % 26`, for *i* = 0 to 5.
      - The tag therefore changes only when the subset's glyphs do (F-09).
  - `internal static class CidWidths`: `static PdfArray Create(TrueTypeFont font, IReadOnlyList<ushort> glyphs)` is `[0 [w0 w1 … wn]]`. Each `wi` is the advance of `glyphs[i]`, scaled.
  - `internal static class FontDescriptor`: `static PdfDictionary Create(TrueTypeFont font, string fontName, PdfReference fontFile)`. Its entries, in this order:
    - `/Type /FontDescriptor`, `/FontName`, `/Flags`;
    - `/FontBBox`, the `head` bounds scaled;
    - `/ItalicAngle`;
    - `/Ascent` and `/Descent`, from `hhea`, scaled;
    - `/CapHeight`, scaled: `CapHeight` when the font has it, otherwise the `yMax` of the glyph for `H` when the font maps it to a glyph with an outline, otherwise the ascent;
    - `/StemV`;
    - `/FontFile2`.
- **Values (F-09):**
  - `/Flags` is 4, plus 1 when `IsFixedPitch`, plus 64 when `IsItalic` or `ItalicAngle != 0`.
  - `/StemV` is `10 + 220 × (w − 50) ÷ 900`, rounded with midpoints away from zero, where `w` is `WeightClass` clamped to 100 to 900. The clamp is this plan's choice: OpenType allows 1 to 1000, and below 55 the formula would go negative.
- **Expected tags.** Computed with Python's `hashlib` while planning, for the base name `TestFont-Regular`:

  | Glyphs from CID 1 | Tag |
  | --- | --- |
  | `[2, 3, 1]` | `WVQWOF` |
  | `[2, 3]` | `PRUZZI` |
  | `[3, 2, 1]` | `NMFKKE` |
  | none | `KOVWTZ` |

- [ ] **Step 1: Failing tests.**
  - `EmbeddedFontNameTests`:
    - `TagsThePostScriptName`: a theory over the table above, with the default font and `glyphs = [0, …]`. `Tagged` is `<tag>+TestFont-Regular`.
    - `CleansTheName`, a theory over `Names(postScript, family, subfamily)`:

      | PostScript | Family | Subfamily | Base name |
      | --- | --- | --- | --- |
      | `Bad[Name](x){y}<z>/a%b cé` | any | any | `BadNamexyzabc` |
      | none | `Test Font` | `Bold` | `TestFont-Bold` |
      | none | `Test Font` | none | `TestFont` |
      | none | none | none | `Font` |
      | `()[]` | any | any | `Font` |
      | 70 × `A` | any | any | 63 × `A` |

  - `FontDescriptorTests`, using `Serialized.Of`:
    - `DescribesTheDefaultFont`. `Create(font, "WVQWOF+TestFont-Regular", new PdfReference(7))` gives exactly:

      `<</Type /FontDescriptor /FontName /WVQWOF+TestFont-Regular /Flags 4 /FontBBox [0 -200 650 800] /ItalicAngle 0 /Ascent 800 /Descent -200 /CapHeight 700 /StemV 96 /FontFile2 7 0 R>>`

    - `ScalesFromUnitsPerEm`: with `UnitsPerEm(2048)`, the bounding box is `[0 -97.6563 317.3828 390.625]`, the ascent `390.625`, the descent `-97.6563` and the cap height `341.7969`.
    - `FlagsFollowPitchAndSlant`, a theory:

      | Font | Flags | ItalicAngle |
      | --- | --- | --- |
      | default | 4 | 0 |
      | `Post(0, 1)` | 5 | 0 |
      | `Os2(4, 400, 0x01)` | 68 | 0 |
      | `MacStyle(2)` | 68 | 0 |
      | `Post(-12 << 16, 0)` | 68 | −12 |
      | `Post(-12 << 16, 1)` | 69 | −12 |

    - `StemVFollowsTheWeight`, a theory over `Os2(4, w, 0x40)`: 400 gives 96, 700 gives 169, 900 gives 218, 0 gives 22, and 1000 gives 218.
    - `CapHeightFallsBack`:
      - With `Os2(1, 400, 0x40)`, a glyph 4 = `SimpleGlyph(600, 0, 0, 600, 720)` and `Map('H', 4)`, the cap height is 720.
      - With `Os2(1, 400, 0x40)` and no `H`, it is 800, the ascent.
    - `WidthsListEveryCid`:
      - `CidWidths.Create(font, [0, 2, 3, 1])` is `[0 [500 600 650 250]]`.
      - With `UnitsPerEm(2048)`, it is `[0 [244.1406 292.9688 317.3828 122.0703]]`.
    - `NotoSansDescriptors`, a theory over the four styles. The flags, StemV, italic angle and cap height match this table, which comes from P1.1's recorded values:

      | Style | Flags | StemV | ItalicAngle | CapHeight |
      | --- | --- | --- | --- | --- |
      | Regular | 4 | 96 | 0 | 714 |
      | Bold | 4 | 169 | 0 | 714 |
      | Italic | 68 | 96 | −12 | 714 |
      | BoldItalic | 68 | 169 | −12 | 714 |

- [ ] **Step 2:** Verify they fail. Expected: CS0103 (the types don't exist).
- [ ] **Step 3:** Implement, with the new `PdfName`s.
- [ ] **Step 4:** Verify they pass, and run the whole suite. Commit `#22 build embedded font names, widths and descriptors`.

### Task 3: Embedded font

**Files:**
- Create: `src/Maviray.Pdf/Writer/PdfFont.cs`, `EmbeddedFont.cs`
- Modify:
  - `src/Maviray.Pdf/Writer/StandardFont.cs`: it derives from `PdfFont`;
  - `FontResources.cs`: keyed by `PdfFont`, and gains `Embed`;
  - `src/Maviray.Pdf/Fonts/TrueTypeFont.cs`: gains `ReadOnlyMemory<byte> FileData`, the font's own copy of its file.
- Test: `tests/Maviray.Pdf.Tests/Writer/EmbeddedFontTests.cs`, `EmbeddedFontObjectsTests.cs`

**Interfaces:**
- Consumes:
  - from Task 1, `ToUnicodeCMap.Build`;
  - from Task 2, `EmbeddedFontName`, `CidWidths.Create` and `FontDescriptor.Create`;
  - from P1.2, `FontSubsetter.Build(TrueTypeFont, IReadOnlyList<ushort>)`, which returns `FontSubset` with `Data`.
- Produces:
  - `internal abstract class PdfFont`: `abstract void Write(PdfFileWriter file, PdfReference reference)`. It writes the font's objects, with its font dictionary at `reference`. `StandardFont.Write` writes `CreateDictionary()` there.
  - `FontResources`:
    - `NameOf(PdfFont)` and `ReferenceOf(PdfFont)`, as before;
    - `EmbeddedFont Embed(TrueTypeFont font)`, which returns the same instance for the same `TrueTypeFont` (by reference);
    - `WriteAll()`, which calls `Write` on every font a page used.
  - `internal sealed class EmbeddedFont : PdfFont`:
    - `TrueTypeFont Font`;
    - `ushort Encode(ushort glyphId, int codePoint)`;
    - `bool IsAssigned(ushort cid)`, true for 1 up to the last CID assigned;
    - `internal FontResources Resources`, the document it belongs to.
- **Encode rules (F-07):**
  - The CID for a new pair is the next one, from 1. The same pair always returns the same CID.
  - A character the font lacks is encoded as glyph 0 with its code point, and gets a CID of its own.
  - Wrong arguments throw `ArgumentOutOfRangeException`, before any CID is assigned:
    - a glyph of `GlyphCount` or more;
    - a code point that isn't a Unicode scalar value (negative, a surrogate, or above `0x10FFFF`).
  - A pair past CID 65,534 throws `InvalidOperationException` containing "65,534".
  - `Encode` after the font is written throws `InvalidOperationException`.
- **Objects (F-09, F-11), written by `Write`** in this order, each with `file.Write` except the last:
  1. the `FontFile2` stream, `<</Length1 n>>`. Its data is the subset from `FontSubsetter.Build(Font, glyphs)`, or `FileData` when `Font.Embedding` is `Whole`;
  2. the font descriptor;
  3. only for `Whole`, the `CIDToGIDMap` stream: for each CID from 0, its glyph as 2 big-endian bytes;
  4. the CIDFont:

     `<</Type /Font /Subtype /CIDFontType2 /BaseFont /N /CIDSystemInfo <</Registry (Adobe) /Ordering (Identity) /Supplement 0>> /FontDescriptor d 0 R /W […] /CIDToGIDMap /Identity>>`

     For `Whole`, `/CIDToGIDMap` is the stream's reference instead;
  5. the ToUnicode stream, `<<>>` with the CMap;
  6. at `reference`, the Type 0 font:

     `<</Type /Font /Subtype /Type0 /BaseFont /N /Encoding /Identity-H /DescendantFonts [c 0 R] /ToUnicode t 0 R>>`

  `N` is `EmbeddedFontName.Tagged`, or `BaseName` for `Whole`. The glyph list passed on is the CIDs' glyphs, from CID 0.
- **Test fixture.** A `FontResources` over a `PdfFileWriter` with `NoCompression`. The test:
  1. embeds the font, encodes the pairs, and calls `NameOf(font)`, which reserves object 1;
  2. calls `WriteAll()` and `Close(reference)`;
  3. reads each object with `PdfFileStructure.ObjectText(n)`.

  Object 1 is the Type 0 font. In a subset, objects 2 to 5 are `FontFile2`, the descriptor, the CIDFont and ToUnicode. For `Whole`, objects 2 to 6 are `FontFile2`, the descriptor, `CIDToGIDMap`, the CIDFont and ToUnicode.

- [ ] **Step 1: Failing tests:**
  - `EmbeddedFontTests`:
    - `CidsAreAssignedInOrderOfFirstUse`:
      - `(2, 'A')`, `(3, 'B')` and `(2, 'A')` give 1, 2 and 1;
      - `(1, ' ')` and `(1, 0xA0)` give 3 and 4: one glyph for two characters gets two CIDs;
      - `IsAssigned` is false for 0 and 5, and true for 1 to 4.
    - `ACharacterTheFontLacksGetsItsOwnCid`: `(0, 0x4E2D)` gives 1, and `(0, 0x4E2E)` gives 2.
    - `EncodeRefusesBadArguments`, a theory: glyph 4, code point −1, `0xD800` and `0x110000` throw `ArgumentOutOfRangeException`, and the next good pair still gets CID 1.
    - `TheLastCidIs65534`: `(2, 0x10000 + i)` for *i* from 0 to 65,533 gives CIDs 1 to 65,534. The next new pair throws `InvalidOperationException` containing "65,534", and an existing pair still returns its CID.
    - `EmbedReturnsOneFontPerTrueTypeFont`: the same font gives the same instance, and another `FontResources` gives a different one.
  - `EmbeddedFontObjectsTests`, with the default builder font and the pairs `(2, 'A')`, `(3, 'B')`, `(1, ' ')`:
    - `WritesTheFiveObjects`, exactly:
      - object 1: `<</Type /Font /Subtype /Type0 /BaseFont /WVQWOF+TestFont-Regular /Encoding /Identity-H /DescendantFonts [4 0 R] /ToUnicode 5 0 R>>`;
      - object 4: `<</Type /Font /Subtype /CIDFontType2 /BaseFont /WVQWOF+TestFont-Regular /CIDSystemInfo <</Registry (Adobe) /Ordering (Identity) /Supplement 0>> /FontDescriptor 3 0 R /W [0 [500 600 650 250]] /CIDToGIDMap /Identity>>`;
      - object 3 contains `/FontFile2 2 0 R`;
      - object 5's stream data is `ToUnicodeCMap.Build([0, 'A', 'B', ' '])`.
    - `FontFile2HoldsTheSubset`: object 2's data equals `FontSubsetter.Build(font, [0, 2, 3, 1]).Data`, and its dictionary has `/Length1` equal to that length.
    - `AFontThatMayNotBeSubsetIsEmbeddedWhole`. With `FsType(0x0100)` and the pairs `(2, 'A')`, `(3, 'B')`:
      - `/BaseFont` is `/TestFont-Regular`, with no tag;
      - object 2's data is the original file;
      - object 4's data is `00 00 00 02 00 03`;
      - object 5 has `/CIDToGIDMap 4 0 R`.
    - `EncodeAfterWritingThrows`: `InvalidOperationException`.
    - `WritingPastTheGlyphLimitThrows`. Glyph 4 = `CompositeGlyph(700, (2, 0x0002), (3, 0x0002))`, with 65,534 pairs `(4, 0x10000 + i)`. `WriteAll()` throws `InvalidOperationException` from the subsetter, because the components make 65,537 glyphs.
- [ ] **Step 2:** Verify they fail. Expected: CS0246 (`EmbeddedFont` doesn't exist).
- [ ] **Step 3:** Implement, with `PdfFont`, the `FontResources` changes and `FileData`. The P0 tests of `FontResources` and `StandardFont` stay green unchanged.
- [ ] **Step 4:** Verify they pass, and run the whole suite. Commit `#22 add embedded fonts and their PDF objects`.

### Task 4: Content streams and the document

**Files:**
- Modify:
  - `src/Maviray.Pdf/Writer/ContentStreamBuilder.cs`: `SetFont(PdfFont, double)`, `FontsUsed` as `IReadOnlyCollection<PdfFont>`, the tracked font as `PdfFont?`, and a new `ShowGlyphs`;
  - `PdfDocumentWriter.cs`: gains `EmbedFont`.
- Test: `tests/Maviray.Pdf.Tests/Writer/ContentStreamGlyphsTests.cs`, `EmbeddedFontDocumentTests.cs`, and the helper `tests/Maviray.Pdf.Tests/Writer/EmbeddedText.cs`

**Interfaces:**
- Consumes: from Task 3, `EmbeddedFont`, `FontResources.Embed` and `PdfFont`.
- Produces:
  - `ContentStreamBuilder.SetFont(PdfFont font, double size)` keeps D-09's tracking. A font this builder's `FontResources` didn't make throws `ArgumentException` naming "another document".
  - `ContentStreamBuilder.ShowGlyphs(ReadOnlySpan<ushort> cids)` writes `<hhhh…> Tj`, with four uppercase hex digits per CID. An empty span writes `<> Tj`. It throws, before writing anything:
    - `InvalidOperationException` with no text object, with no font set, or with the standard font current;
    - `ArgumentException` for CID 0 or a CID the font hasn't assigned.
  - `ShowText` with an embedded font current throws `InvalidOperationException`.
  - `PdfDocumentWriter.EmbedFont(TrueTypeFont font)` returns `_fonts.Embed(font)`, and throws once the document is closed.
  - Tests: `EmbeddedText.Encode(EmbeddedFont font, string text)` returns, for each `Rune`, `font.Encode(font.Font.GetGlyphId(rune.Value), rune.Value)`.

- [ ] **Step 1: Failing tests:**
  - `ContentStreamGlyphsTests`, using a builder over a `FontResources` and the default builder font, with CIDs 1 and 2 assigned:
    - `ShowGlyphsWritesFourHexDigitsPerCid`: `BeginText().SetFont(font, 24).ShowGlyphs([1, 2, 1]).EndText()` writes `BT\n/F1 24 Tf\n<000100020001> Tj\nET\n`.
    - `ShowGlyphsRefusals`. Each refusal leaves the content unchanged:
      - CID 0 and CID 3 throw `ArgumentException`;
      - no font, or Helvetica current, throws `InvalidOperationException`;
      - outside a text object, it throws `InvalidOperationException`.
    - `ShowTextRefusesAnEmbeddedFont`.
    - `SetFontRefusesAFontFromAnotherDocument`: a font from another `FontResources` throws `ArgumentException`.
  - `EmbeddedFontDocumentTests`, writing documents with `PdfDocumentWriterTests.Write`:
    - `StandardAndEmbeddedFontsShareOneSequenceOfNames`: on one page, Helvetica then Noto Sans Regular give `/F1` and `/F2` in the page's `/Font` resources.
    - `EachPageListsOnlyTheFontsItSet`: page 1 sets Noto Sans and page 2 sets Helvetica, so page 2's resources have no `/F1`.
    - `AFontOnTwoPagesIsWrittenOnce`: the file has one `/Subtype /Type0`.
    - `AFontNoPageSetIsNotWritten`: after `EmbedFont` with no `SetFont`, the file has no `/Type0`.
    - `WritingTwiceGivesTheSameBytes`: the same Noto Sans line, written twice with the same file ID, gives equal bytes.
    - `EmbedFontAfterCloseThrows`.
    - `ClosingPastTheGlyphLimitThrows`: Task 3's limit case through a page throws `InvalidOperationException` from `Close`.
- [ ] **Step 2:** Verify they fail. Expected: CS1503 or CS1061 (`SetFont` takes only `StandardFont`, and `ShowGlyphs` and `EmbedFont` don't exist).
- [ ] **Step 3:** Implement. The P0 builder and document tests stay green unchanged.
- [ ] **Step 4:** Verify they pass, and run the whole suite. Commit `#22 draw embedded fonts in content streams`.

### Task 5: pdffonts, and a line of Noto Sans

**Files:**
- Create: `tests/Maviray.Pdf.Tests/Validation/PdfFontReport.cs`; test `tests/Maviray.Pdf.Tests/Writer/EmbeddedFontValidationTests.cs`
- Modify: `tests/Maviray.Pdf.Tests/Validation/Poppler.cs`, `ValidatorSelfTests.cs`

**Interfaces:**
- `Poppler.ListFonts(string path)` returns `IReadOnlyList<PdfFontReport>`, from `pdffonts <path>`.
  - The column boundaries come from the dashed line under the header, and the columns are found by their header names: `name`, `type`, `emb`, `sub` and `uni`. Poppler adds an `encoding` column; xpdf's pdffonts, which Git Bash may supply, doesn't.
  - A non-zero exit fails the test with the tool's output.
- `PdfFontReport(string Name, string Type, bool Embedded, bool Subset, bool Unicode)`: each flag is `yes` or `no`.

- [ ] **Step 1: Failing tests:**
  - `ValidatorSelfTests.PdffontsReportsHelveticaAsNotEmbedded`. It locates `pdffonts` first. For a Helvetica page, it reports one font: `Helvetica`, not embedded.
  - `EmbeddedFontValidationTests`:
    - `ALineOfNotoSansPassesQpdfPdffontsAndPdftotext`. On an A4 page at 24 pt, `Grüße aus Köln: Äpfel, Öl und Übermaß` in Noto Sans Regular, compressed:
      - it passes `Qpdf.Check`;
      - pdffonts reports one font: type `CID TrueType`, embedded, subset, Unicode, with a name matching `^[A-Z]{6}\+NotoSans-Regular$`;
      - `Poppler.ExtractText` returns the line exactly.
    - `ACharacterTheFontLacksStillCopiesOut`: `Köln 中` in Noto Sans Regular. `中` has no glyph in the font, and pdftotext still returns `Köln 中`.
    - `AWholeFontIsEmbeddedButNotSubset`. The default builder font with `FsType(0x0100)` and the text `AB A`:
      - it passes `Qpdf.Check`;
      - pdffonts reports `TestFont-Regular`, embedded, not subset, Unicode;
      - pdftotext returns `AB A`.
- [ ] **Step 2:** Verify they fail. Expected: CS0117 (`Poppler.ListFonts` doesn't exist).
- [ ] **Step 3:** Implement. Locally, a missing tool skips with its message. CI runs everything.
- [ ] **Step 4:** Verify the whole suite passes. Commit `#22 check embedded fonts with pdffonts`.

### Task 6: Records

- [ ] Write `docs/releases/0.1.0/execution-logs/2026-11-23-issue-22-embedded-truetype-fonts.md`.
- [ ] Set #22's row in the release index to "In review", with the plan and the log.
- [ ] Commit `#22 add the P1.3 plan and execution log`.
- [ ] Push, and open the PR into `develop` with `Refs #22`. Its description says it is stacked on #25, #24 and #19.
- [ ] Record the first CI run in the log: tests run, and 0 skipped.
