# P1.2 TrueType subsetter: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An internal `FontSubsetter` that turns a loaded `TrueTypeFont` and the list of glyphs a document used into a small, valid TrueType file. The file holds those glyphs, in that order, plus the components they need, and keeps the hinting. fontTools confirms the subsets of all four Noto Sans styles.

**Architecture:** Three small parts, each tested on its own:
- `GlyphClosure` turns the requested list into the subset's full glyph list. It appends missing components and refuses cycles, deep nesting and oversized subsets.
- `FontSubsetter` copies each glyph's bytes, rewrites the component IDs, and builds the new `loca`, `hmtx`, `hhea`, `maxp`, `head` and `post`.
- `FontFileAssembler` writes the tables as an sfnt file, with checksums and `checkSumAdjustment`.

The tests read the files they produce with their own small reader. A second fontTools subcommand compares every subset glyph with its original.

**Tech Stack:** .NET 10, C#, xUnit v3, Python 3 with fontTools (`python3-fonttools` in CI, already installed by P1.1).

**Spec:** [P1 Fonts](../specs/2026-11-20-p1-fonts-design.md), decision F-08, with F-05 (cycles and depth), F-07 (the 65,535 limit) and F-13 (the subset check). Work item [#21](https://github.com/Maviray/pdf/issues/21). The branch `feature/21-truetype-subsetter` is stacked on `feature/20-truetype-parser` (PR #24).

## Global Constraints

- Every P1.1 constraint holds:
  - every type in `src/` is `internal`, in `Maviray.Pdf.Fonts`, one type per file;
  - no package references in `src/`;
  - font data errors throw `InvalidDataException`, with the table tag in single quotes;
  - offsets and lengths are computed in `long`;
  - C# with backslashes is written with the file tools;
  - expected values come from the spec, the OpenType specification 1.9.1, fontTools, or a throwaway read of the files while planning, never from the code under test.
- The subsetter reads the loaded font only, through `TrueTypeFont`. It never interprets glyph outlines or instructions: only composite records, to find and rewrite component IDs.
- Wrong arguments from internal callers throw `ArgumentException` or `ArgumentOutOfRangeException`. Limits a document can reach, such as more than 65,535 glyphs or a font that may not be subset, throw `InvalidOperationException` (F-07).
- The subset's bytes depend only on the font's bytes and the glyph list. No clock, no hash-ordered collection decides an order.
- Each task ends with one commit, subject `#21 <summary>`. The records are the last commit.

## Review Focus

1. **Damage the subset reaches.** A glyph in the subset can be damaged in three ways: a truncated composite record, a component past the font, or data shorter than a glyph header. Any of them throws `InvalidDataException` whose message starts with the font's name, and nothing else escapes. Pinned in Task 3 and by the damage sweep in Task 5.
2. **Cycles and nesting at the limit.** These are refused without unbounded recursion:
   - a glyph naming itself;
   - two glyphs naming each other;
   - a chain of 17 composites.

   A chain of exactly 16 composites is accepted. Pinned in Task 3.
3. **Subsets at the limits.** Exactly 65,535 glyphs, appended components included, is accepted, and one more throws `InvalidOperationException`. A subset whose file would pass `Array.MaxLength` also throws it, before any glyph is copied. Pinned in Tasks 3 and 4.
4. **Glyph data not on 4-byte boundaries.** The data is copied and zero-padded, and every `loca` entry is a multiple of 4. Pinned in Task 4. Three Noto styles each have one such glyph, which Task 6's reversed subset covers.
5. **A component that is also listed directly, or listed twice.** It refers to the glyph's first subset ID and is never appended. Pinned in Tasks 3 and 4.

---

### Task 1: Left side bearings

The subset's `hmtx` has a full entry for every glyph, so the subsetter needs each glyph's left side bearing, including those past `numberOfHMetrics`.

**Files:**
- Modify: `src/Maviray.Pdf/Fonts/HorizontalMetricsTable.cs`, `src/Maviray.Pdf/Fonts/TrueTypeFont.cs`
- Modify (tests): `tests/Maviray.Pdf.Tests/Fonts/TestFontBuilder.cs`. `hmtx` now writes each glyph's `xMin` as its left side bearing: the `Int16` at offset 2 of its data, or 0 for an empty glyph.
- Test: `tests/Maviray.Pdf.Tests/Fonts/TrueTypeFontTests.cs`

**Interfaces:**
- Produces:
  - `int TrueTypeFont.GetLeftSideBearing(ushort glyphId)`. A glyph ID of `GlyphCount` or more throws `ArgumentOutOfRangeException`, as `GetAdvanceWidth` does.
  - `int HorizontalMetricsTable.LeftSideBearingOf(int glyph)`. A glyph below `numberOfHMetrics` uses the `Int16` at `4 × glyph + 2`. Any later glyph uses the one at `4 × numberOfHMetrics + 2 × (glyph − numberOfHMetrics)`.

- [ ] **Step 1: Failing tests:**
  - `LeftSideBearingsComeFromHmtx`. The font is `ClearGlyphs()`, then `SimpleGlyph(500, 50, 0, 450, 700)`, `SimpleGlyph(600, 30, 0, 600, 700)`, `SimpleGlyph(650, -20, 0, 650, 700)` and `NumberOfHMetrics(2)`. It gives:
    - left side bearings 50, 30 and −20;
    - advance 600 for glyph 2.
  - `LeftSideBearingRefusesAGlyphPastTheFont`: glyph 4 of the default font throws `ArgumentOutOfRangeException`.
- [ ] **Step 2:** Verify they fail. Expected: CS1061 (`GetLeftSideBearing` doesn't exist).
- [ ] **Step 3:** Implement. Change the builder.
- [ ] **Step 4:** Verify they pass, and run the whole suite. Commit `#21 read left side bearings`.

### Task 2: Font file assembler

**Files:**
- Create: `src/Maviray.Pdf/Fonts/FontFileAssembler.cs`
- Test: `tests/Maviray.Pdf.Tests/Fonts/FontFileAssemblerTests.cs`, `tests/Maviray.Pdf.Tests/Fonts/TestFontFile.cs`

**Interfaces:**
- Produces `internal static class FontFileAssembler`:
  - `static uint Checksum(ReadOnlySpan<byte> data)`: the sum, modulo 2³², of the data as big-endian 32-bit words. The last word is padded with zeros.
  - `static byte[] Assemble(uint sfntVersion, IReadOnlyDictionary<string, byte[]> tables)`. Rules (OpenType "Organization of an OpenType font"):
    - The tables are sorted by tag, ordinal.
    - The header is `sfntVersion`, `numTables`, then the three search fields: `searchRange` (the largest power of 2 ≤ `numTables`, × 16), `entrySelector` (log₂ of that power) and `rangeShift` (`numTables × 16 − searchRange`).
    - Each 16-byte record holds the tag, the checksum, the offset and the unpadded length.
    - The data follows in record order. Each table starts on a multiple of 4 and is padded with zeros.
    - When `head` is present and at least 12 bytes long, its copy has bytes 8 to 11 zeroed before its checksum is taken. Once the file is complete, those bytes are set to `0xB1B0AFBA − Checksum(file)`.
    - The input arrays are never changed.
    - A tag that isn't 4 characters from `0x20` to `0x7E`, or an empty dictionary, throws `ArgumentException`.
- Produces (tests) `TestFontFile(byte[] file)`, a reader for any sfnt file the tests produce. It reads with `BinaryPrimitives`, independently of `TableDirectory`:
  - `uint SfntVersion`, `(ushort SearchRange, ushort EntrySelector, ushort RangeShift) SearchFields`, `IReadOnlyList<string> Tags` (record order);
  - `byte[] Table(string tag)`, `int OffsetOf(string tag)`, `uint RecordChecksum(string tag)`;
  - `int GlyphCount` (`maxp`), `int NumberOfHMetrics` (`hhea`), and `uint[] Loca` (long entries);
  - `byte[] Glyph(int glyph)`, `(ushort Advance, short LeftSideBearing) Metrics(int glyph)` and `ushort[] ComponentIds(int glyph)`. `ComponentIds` uses `CompositeGlyph.ReadComponents`.

- [ ] **Step 1: Failing tests** (`FontFileAssemblerTests`):
  - `ChecksumSumsBigEndianWords`, a theory:

    | Bytes | Checksum |
    | --- | --- |
    | none | `0` |
    | `01 02 03 04` | `0x01020304` |
    | `01 02 03 04 05` | `0x06020304` |
    | `FF FF FF FF 00 00 00 02` | `0x00000001` |

  - `SearchFieldsFollowTheTableCount`, a theory over `n` tables tagged `t000`, `t001` and so on, each holding 4 bytes:

    | Tables | `searchRange` | `entrySelector` | `rangeShift` |
    | --- | --- | --- | --- |
    | 1 | 16 | 0 | 0 |
    | 7 | 64 | 2 | 48 |
    | 8 | 128 | 3 | 0 |
    | 11 | 128 | 3 | 48 |
    | 16 | 256 | 4 | 0 |

  - `TablesAreSortedAlignedAndPadded`. Tables `post` (5 bytes), `OS/2` (3), `cvt ` (4) and `glyf` (1):
    - the records are `OS/2`, `cvt `, `glyf`, `post`;
    - the first offset is 76, and every offset is a multiple of 4;
    - each table's bytes are its input, and the record's length is the input's length;
    - the bytes up to the next table are zeros, and the file ends at the last table's padded end;
    - each record's checksum is `Checksum` of its input.
  - `HeadCarriesTheChecksumAdjustment`. The tables are a 54-byte `head` with `DE AD BE EF` at bytes 8 to 11 and its other bytes `i % 251`, and a 6-byte `glyf`:
    - `Checksum` of the whole file is `0xB1B0AFBA`;
    - the `head` record's checksum is that of `head` with bytes 8 to 11 zeroed;
    - the input `head` still holds `DE AD BE EF`.
  - `ReadsBackWithTheTableDirectory`: `TableDirectory.Read` on the result gives the same tags and bytes.
  - `RefusesBadTags`, a theory over `abc`, `abcde`, a tag containing `0x01`, and an empty dictionary: `ArgumentException`.
- [ ] **Step 2:** Verify they fail. Expected: CS0103 (`FontFileAssembler` doesn't exist).
- [ ] **Step 3:** Implement the assembler and `TestFontFile`.
- [ ] **Step 4:** Verify they pass, and run the whole suite. Commit `#21 add the font file assembler`.

### Task 3: Glyph closure

**Files:**
- Create: `src/Maviray.Pdf/Fonts/GlyphClosure.cs`
- Modify (tests): `TestFontBuilder.cs` gains `RawGlyph(ushort advance, byte[] data)`, a glyph with exactly these bytes.
- Test: `tests/Maviray.Pdf.Tests/Fonts/GlyphClosureTests.cs`

**Interfaces:**
- Consumes: `TrueTypeFont.GlyphCount`, `GetGlyphHeader`, `GetGlyphData`, and `CompositeGlyph.ReadComponents`.
- Produces `internal static class GlyphClosure`:
  - `const int MaxGlyphs = 65535` and `const int MaxNesting = 16`.
  - `static ushort[] Resolve(TrueTypeFont font, IReadOnlyList<ushort> glyphs)`. It returns the original glyph of every subset glyph: the input, unchanged and with its duplicates, followed by every component not yet in the list.
- **Argument rules:**
  - a null `glyphs` throws `ArgumentNullException`;
  - an empty list, or a first entry other than 0 (`.notdef`), throws `ArgumentException`;
  - an entry of `GlyphCount` or more throws `ArgumentOutOfRangeException` naming `glyphs`.
- **Visit order**, which fixes the subset's bytes. The subset glyphs are visited in index order, including those appended along the way:

  ```text
  for i = 0, 1, … while i < list.Count:
      header = font.GetGlyphHeader(list[i])               // InvalidDataException below 10 bytes
      if header is composite:
          check the nesting under list[i]
          for each component c, in record order:
              if c.GlyphId is not in the list: append it
  ```

- **Nesting.** A simple or empty glyph has depth 0, and a composite has 1 plus the largest depth of its components. A visited glyph deeper than `MaxNesting` throws `InvalidDataException`, naming `'glyf'` and containing "deeper than 16".
  - A glyph that reaches itself throws `InvalidDataException`, naming `'glyf'`, containing "cycle", and listing the glyph IDs around it.
  - A cycle longer than 16 glyphs may meet the depth rule first; either message is correct.
  - The walk never recurses more than `MaxNesting + 1` frames. Each original glyph's components are read once, and each glyph's depth is computed once.
- **Limit.** As soon as the list would pass `MaxGlyphs` entries, `Resolve` throws `InvalidOperationException` containing "65,535" (F-07).
- **Test fonts.** The default builder font has glyphs 0 to 3, with 2 and 3 simple. Composites are added from glyph 4 on, with flags `0x0002` (arguments are x and y offsets) unless a test says otherwise. `C(a, b)` below means `CompositeGlyph(700, new TestComponent(a, 0x0002), new TestComponent(b, 0x0002))`.

- [ ] **Step 1: Failing tests:**
  - `SimpleGlyphsResolveToThemselves`: `[0, 3, 2, 3]` gives `[0, 3, 2, 3]`.
  - `ComponentsNotInTheListAreAppended`: glyph 4 = `C(2, 3)`. `[0, 4, 3]` gives `[0, 4, 3, 2]`.
  - `NestedComponentsAreAppendedInVisitOrder`: glyph 4 = `C(2, 3)` and glyph 5 = `C(4, 2)`. `[0, 5]` gives `[0, 5, 4, 2, 3]`.
  - `AComponentListedTwiceIsNotAppended`: glyph 4 = `C(2, 3)`. `[0, 4, 2, 2, 3]` comes back unchanged.
  - `RefusesACycle`, a theory with two cases. In both, `[0, 4]` throws `InvalidDataException` containing `'glyf'` and "cycle".
    - Glyph 4 has the single component 4.
    - Glyph 4 has the single component 5, and glyph 5 has the single component 4.
  - `NestingOf16IsAcceptedAnd17IsRefused`. Glyphs 4 to `3 + n` are a chain: each has the single component of the next glyph, and the last has component 2.
    - With `n` = 16, `[0, 4]` gives `[0, 4, 5, …, 19, 2]`, 18 entries.
    - With `n` = 17, it throws `InvalidDataException` containing "deeper than 16".
  - `DamagedGlyphsInTheSubsetAreInvalid`, a theory with two cases. In both, `[0, 4]` throws `InvalidDataException` containing `'glyf'`.
    - Glyph 4 = `C(2, 99)`.
    - Glyph 4 = `RawGlyph(500, [0x00, 0x01, 0x00, 0x00])`.
  - `BrokenGlyphsOutsideTheSubsetDoNotMatter`: glyph 4 has the single component 4, and `[0, 2, 3]` resolves.
  - `ArgumentsAreChecked`: `[]` and `[2]` throw `ArgumentException`, and `[0, 4]` in the 4-glyph default font throws `ArgumentOutOfRangeException`.
  - `MoreThan65535GlyphsAreRefused`. Glyph 4 = `C(2, 3)`.
    - `[0]` followed by 65,534 entries of 4 throws `InvalidOperationException` containing "65,535", because 2 and 3 would be appended.
    - `[0, 2, 3]` followed by 65,532 entries of 4 gives 65,535 entries.
  - `NotoSansADieresisBringsItsComponents`, a theory over the four styles: `[0, 134]` gives `[0, 134, 36, 106]`. Glyph 134 is "Ä" and is made of "A" (36) and a mark (106), both simple. These values were read from the files with a throwaway script while planning, and Task 6's fontTools check is the independent confirmation.
- [ ] **Step 2:** Verify they fail. Expected: CS0103 (`GlyphClosure` doesn't exist).
- [ ] **Step 3:** Implement, with `RawGlyph`.
- [ ] **Step 4:** Verify they pass, and run the whole suite. Commit `#21 resolve the glyphs a subset needs`.

### Task 4: Subsetter

**Files:**
- Create: `src/Maviray.Pdf/Fonts/FontSubsetter.cs`, `src/Maviray.Pdf/Fonts/FontSubset.cs`
- Modify (tests): `TestFontBuilder.cs` gains `GlyphAlignment(int bytes)`. The default is 4, and 1 writes each glyph's data unpadded, which only long `loca` can describe.
- Test: `tests/Maviray.Pdf.Tests/Fonts/FontSubsetterTests.cs`

**Interfaces:**
- Consumes: `GlyphClosure.Resolve`, `FontFileAssembler.Assemble`, `CompositeGlyph.ReadComponents`, and from `TrueTypeFont`: `GetGlyphData`, `GetAdvanceWidth`, `GetLeftSideBearing`, `TryGetTable`, `Embedding`, `PostScriptName`.
- Produces:
  - `internal sealed class FontSubset`: `ReadOnlyMemory<byte> Data` (the TrueType file) and `IReadOnlyList<ushort> OriginalGlyphIds` (the original glyph of each subset glyph, from `GlyphClosure`).
  - `internal static class FontSubsetter`: `static FontSubset Build(TrueTypeFont font, IReadOnlyList<ushort> glyphs)`. `glyphs[n]` is the original glyph of CID *n*, from CID 0 (F-07, F-08).
- **Rules:**
  - A font whose `Embedding` is `Whole` throws `InvalidOperationException` containing "may not be subset" (F-06, F-11).
  - The list is resolved by `GlyphClosure`. An `InvalidDataException` from it, or from reading a glyph, is rethrown with `<PostScriptName>: ` in front, as `TrueTypeFont.Load` does, with the original as the inner exception.
  - The file's length is computed in `long` before any glyph is copied. Above `Array.MaxLength`, `Build` throws `InvalidOperationException` containing "too large".
- **Tables:**
  - `glyf`: each subset glyph's original bytes. Every component's glyph ID, at its `GlyphIdOffset`, is replaced by the first index of that original glyph in the resolved list. Each glyph is zero-padded to a multiple of 4.
  - `loca`: long format, count + 1 entries.
  - `hmtx`: one `(advance, left side bearing)` pair per subset glyph.
  - `hhea`, `maxp`: the original bytes, with `numberOfHMetrics` (offset 34) or `numGlyphs` (offset 4) set to the subset's count.
  - `head`: the original bytes, with `indexToLocFormat` (offset 50) set to 1. The assembler sets `checkSumAdjustment`.
  - `post`: when the font has one, its first 32 bytes, with the version (offset 0) set to `0x00030000`. Without one, there is none.
  - `cvt `, `fpgm`, `prep`, `gasp`: copied when present.
  - Nothing else.
  - `sfntVersion` is `0x00010000`, which also covers a font read with the Apple `true` version.
- **Test fonts.** As Task 3, with the same `C(a, b)` notation, read with `TestFontFile`.

- [ ] **Step 1: Failing tests:**
  - `GlyphsAreCopiedInSubsetOrder`: `[0, 3, 2]` gives:
    - three glyphs whose bytes are the original glyphs 0, 3 and 2;
    - `OriginalGlyphIds` `[0, 3, 2]`;
    - `maxp.numGlyphs` 3.
  - `AGlyphUsedTwiceIsCopiedTwice`: `[0, 2, 2]` gives glyphs 1 and 2 with equal bytes at different `loca` offsets.
  - `ComponentIdsAreRewritten`. Glyph 4 = `C(2, 3)`, glyph 5 = `C(4, 2)`, and `[0, 5, 3]` resolves to `[0, 5, 3, 4, 2]`:
    - subset glyph 1 has components `[3, 4]`;
    - subset glyph 3 has components `[4, 2]`;
    - with the component IDs written back to the original ones, each glyph's bytes equal the original's.
  - `EveryRecordLengthIsRewritten`, a theory over the flags `0x0003`, `0x000A`, `0x0042`, `0x0082` and `0x0102`. Glyph 4 has components 2 and 3 with flags `f`. `[0, 4, 3]` resolves to `[0, 4, 3, 2]`, and subset glyph 1 has components `[3, 2]`.
  - `GlyphDataIsPaddedToFourBytes`: `GlyphAlignment(1)` and `[0, 2, 3]` give:
    - `loca` entries that are all multiples of 4;
    - for each glyph, its original 34 bytes followed by 2 zeros.
  - `MetricsHaveOneFullEntryPerGlyph`. Task 1's three-glyph font, with `[0, 2, 1]`, gives:
    - `numberOfHMetrics` 3 and `numGlyphs` 3;
    - `hmtx` entries `(500, 50)`, `(600, −20)` and `(600, 30)`.
  - `HeaderTablesKeepEverythingButTheirCounts`. With `IndexToLocFormat(0)` and `[0, 2]`:
    - `head` equals the original except bytes 8 to 11 and 50 to 51, which are `00 01`;
    - `hhea` equals the original except bytes 34 to 35;
    - `maxp` equals the original except bytes 4 to 5.
  - `PostBecomesFormat3`. `Table("post", …)` holds a 42-byte format 2 table: version `0x00020000`, `italicAngle` `0xFFF40000`, then known bytes. The subset's `post` is its first 32 bytes with the version `00 03 00 00`.
  - `WithoutPostTheSubsetHasNone`: `Without("post")`.
  - `KeepsTheHintingTablesAndDropsTheRest`. The default font gains, with distinct bytes each:
    - `cvt `, `fpgm`, `prep` and `gasp`, which are kept;
    - `kern`, `GPOS`, `GSUB`, `GDEF`, `hdmx`, `LTSH` and `VDMX`, which are dropped.

    The subset's tags are `cvt `, `fpgm`, `gasp`, `glyf`, `head`, `hhea`, `hmtx`, `loca`, `maxp`, `post`, `prep`, and each kept table's bytes equal the original's.
  - `TheFileIsAValidSfnt`. On the subset of the previous test:
    - `sfntVersion` is `0x00010000`, and the search fields are 128, 3 and 48;
    - every record's checksum is correct, with `head`'s bytes 8 to 11 zeroed;
    - `FontFileAssembler.Checksum` of the file is `0xB1B0AFBA`.
  - `TheSameInputGivesTheSameBytes`:
    - `Build` twice on the default font with `[0, 3, 2]` gives equal bytes;
    - so do two separate loads of Noto Sans Regular with `[0, 134, 3]`.
  - `RefusesAFontThatMayNotBeSubset`: `FsType(0x0100)` throws `InvalidOperationException`.
  - `RefusesASubsetTooLargeForOneFile`. Glyph 4 = `RawGlyph(500, b)`, where `b` is 40,000 bytes starting `00 00` (no contours). `[0]` followed by 65,534 entries of 4 throws `InvalidOperationException` containing "too large", because 65,534 × 40,000 bytes passes `Array.MaxLength`.
  - `DamageMessagesNameTheFont`. Glyph 4 has the single component 4, and `[0, 4]` throws `InvalidDataException`:
    - its message starts with `TestFont-Regular: ` and contains "cycle";
    - its inner exception is an `InvalidDataException`.
  - `NotoSansSubsetKeepsHintingAndItsAngle`, a theory over the four styles. `[0, 134]` gives:
    - the 11 tags above;
    - `cvt `, `fpgm`, `prep` and `gasp` equal to the font's own;
    - `post` of 32 bytes, version 3, with bytes 4 to 31 equal to the font's own.
- [ ] **Step 2:** Verify they fail. Expected: CS0103 (`FontSubsetter` doesn't exist).
- [ ] **Step 3:** Implement, with `GlyphAlignment`.
- [ ] **Step 4:** Verify they pass, and run the whole suite. Commit `#21 build TrueType subsets`.

### Task 5: Damage the subset reaches

**Files:** test `tests/Maviray.Pdf.Tests/Fonts/DamagedFontTests.cs`

- [ ] **Step 1: Tests.**
  - The format 12 starting font gains a nested composite: glyph 5 = `CompositeGlyph(800, new TestComponent(4, 0x0002), new TestComponent(2, 0x0002))`.
  - `Exercise` gains a step. When `font.Embedding` is `Subset`, it builds a subset of every glyph, `[0, 1, …, GlyphCount − 1]`.
    - An `InvalidDataException` is documented and is caught.
    - A subset that builds must be readable by `TableDirectory.Read`, and its `maxp.numGlyphs` must equal `GlyphCount`, since every glyph is already listed. A broken promise throws `InvalidOperationException`, which the sweep reports.
- [ ] **Step 2:** The red step is a mutant, as in P1.1. Remove the component range check (`glyphId >= glyphCount`) from `CompositeGlyph.ReadComponents`. Expected: `EveryByteChangeLoadsOrThrowsTheDocumentedExceptions` fails with `ArgumentOutOfRangeException` from the subsetter. Then restore the check.
- [ ] **Step 3:** Fix every escape the sweep finds, in the type that owns it.
- [ ] **Step 4:** Verify the tests pass, and run the whole suite. Commit `#21 sweep damaged fonts through the subsetter`.

### Task 6: The fontTools subset check

**Files:**
- Modify: `tests/Maviray.Pdf.Tests/Validation/fonttools_check.py`, `FontTools.cs`, `ValidatorSelfTests.cs`
- Test: `tests/Maviray.Pdf.Tests/Fonts/FontToolsSubsetTests.cs`

**Interfaces:**
- `FontTools.CheckSubset(string originalPath, string subsetPath, IReadOnlyList<ushort> originalGlyphIds)` returns the number of glyphs fontTools compared.
  - It writes the IDs, space-separated ASCII, to `<subsetPath>.glyphs.txt`.
  - It runs `fonttools_check.py subset <original> <subset> <ids file>` with `-I`.
  - A non-zero exit fails the test with the script's output, as `Dump` does.
- The script's `subset` command:
  1. Loads the subset with `TTFont(path, checkChecksums=2)`, so a wrong table checksum raises. Then it decompiles every table: `for tag in font.keys(): font[tag]`.
  2. Checks that `maxp.numGlyphs` and `hhea.numberOfHMetrics` both equal the number of IDs.
  3. Compares each subset glyph *i* with original glyph `ids[i]`, both looked up by their fonts' glyph order:
     - the same `numberOfContours`;
     - the same coordinates, end points and flags from `getCoordinates(glyf)`, which resolves components and their transforms;
     - for a composite, the same components in order: each subset component, mapped back through `ids`, names the original component, and every other attribute is equal;
     - the same instructions (`program.getBytecode()`, when either glyph has a program);
     - the same `hmtx` entry.
  4. Prints `{"glyphs": n}`. On any difference, it prints the first 20 differences to stderr and exits with 1.
- `FontToolsSubsetTests`:
  - `SubsetsOfNotoSansPassTheFontToolsCheck`, a theory over the four styles × two lists, which are:
    - **gate text:** 0; then, for the six lines of F-15 joined, the glyph of each distinct (glyph, code point) pair in order of first use, as F-07 assigns CIDs; then the glyph of `G` again, standing for a second character drawn with the same glyph.
    - **every glyph reversed:** `[0, GlyphCount − 1, …, 1]`, which rewrites the component IDs of every composite in the font.

    Each subset is saved with `GeneratedFiles.SaveAs(…, "-<style>-<list>.ttf")`. The result is the count of `OriginalGlyphIds`.
  - `SyntheticNestedSubsetPassesTheFontToolsCheck`. The font has:
    - glyph 4 = components `(2, 0x0003, 100, −50)` and `(3, 0x000A)`;
    - glyph 5 = components `(4, 0x0042)`, `(2, 0x0082)` and `(3, 0x0102)`.

    It is saved as `-original.ttf`, and its subset of `[0, 5, 3]` as `-subset.ttf`. The check passes for 5 glyphs.
- `ValidatorSelfTests` gains two facts, each calling `FontTools.Python()` first so they skip without fontTools. Both use the default font's subset of `[0, 2, 3]`, and each throws `XunitException`:
  - `FontToolsRejectsASubsetWithTheWrongGlyphs`: checked against `[0, 3, 2]`.
  - `FontToolsRejectsASubsetWithABadChecksum`: one byte inside its `glyf` table changed (`TestFontFile.OffsetOf("glyf")` + 12).

- [ ] **Step 1: Failing tests.**
- [ ] **Step 2:** Verify they fail. Expected: CS0117 (`FontTools.CheckSubset` doesn't exist).
- [ ] **Step 3:** Implement. Locally, without fontTools, every test that runs it skips with the install message. The PR's CI run is where they first run.
- [ ] **Step 4:** Verify the whole suite passes locally, with those tests skipped. Commit `#21 check subsets with fontTools`.

### Task 7: Records

- [ ] Write `docs/releases/0.1.0/execution-logs/2026-11-23-issue-21-truetype-subsetter.md`.
- [ ] Set #21's row in the release index to "In review", with the plan and the log.
- [ ] Commit `#21 add the P1.2 plan and execution log`.
- [ ] Push `feature/21-truetype-subsetter`, and open the PR into `develop` with `Refs #21`. Its description says it is stacked on #24 and #19.
- [ ] Record the first CI run in the log: tests run, and 0 skipped.
