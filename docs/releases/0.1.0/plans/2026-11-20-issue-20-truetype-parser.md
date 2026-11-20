# P1.1 TrueType parser: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An internal, immutable `TrueTypeFont` that reads static TrueType fonts (metrics, advance widths, the Unicode `cmap`, glyph locations and composite components), refuses what the library can't embed, and throws only `InvalidDataException` or `NotSupportedException` for bad input. fontTools confirms it on all four Noto Sans styles.

**Architecture:** A bounds-checked reader over each table's bytes feeds one small parser type per table. `TrueTypeFont.Load` copies the file, reads the table directory, then the `name` table (so later messages can name the font), then everything else, and keeps the parsed values. The tests build their own small fonts with a test helper, and use the committed Noto Sans files for real data. A Python script runs fontTools on the same files, as an independent check.

**Tech Stack:** .NET 10, C#, xUnit v3, `System.Text.Json` in the tests, Python 3 with fontTools (`python3-fonttools` on `ubuntu-latest`).

**Spec:** [P1 Fonts](../specs/2026-11-20-p1-fonts-design.md), decisions F-01 to F-06, F-12, F-13 (parser check) and F-14. Work item [#20](https://github.com/Maviray/pdf/issues/20).

## Global Constraints

- Every type in `src/` is `internal`, in `Maviray.Pdf.Fonts`, under `src/Maviray.Pdf/Fonts/`. One type per file, no partial classes, no reflection, no package references in `src/`, warnings as errors.
- Font data errors throw `InvalidDataException`. Fonts the library won't use throw `NotSupportedException`. Nothing else escapes for any file content: an out-of-range read, an overflow or an `EndOfStreamException` is a bug. Wrong arguments from internal callers (a glyph ID of `GlyphCount` or more) throw `ArgumentOutOfRangeException`, which is a caller bug, not a font problem.
- An `InvalidDataException` message contains the table tag in single quotes, for example `'hhea'`. Once the `name` table has been read, every message from `Load` starts with `<PostScriptName>: `.
- Offsets and lengths are computed in `long` or checked before adding, so a huge value can't overflow into a valid-looking one.
- C# that contains backslashes is written with the file tools, never through a shell here-document (P0.1 log).
- Expected values come from the spec, the OpenType specification, or fontTools. A value our own code computes is never the expectation for itself.
- The work is committed at the end, as three commits: the font files and notices, the code, and the records.

## Review Focus

1. **Arbitrary bytes.** Any truncation or single-byte change of a valid font loads or throws one of the two documented exceptions, and the loaded font's lookups don't throw either. Pinned in Task 6.
2. **A `cmap` that points outside itself.** A format 4 `idRangeOffset` past the subtable, or format 12 groups that overlap, are refused at load, not at lookup. Pinned in Task 3.
3. **A `loca` that runs backwards or past `glyf`.** Refused at load. Pinned in Task 4.
4. **`fsType` with several bits set.** The least restrictive of bits 1 to 3 wins, and bit 9 refuses whatever else is set. Pinned in Task 4.
5. **A composite with every flag that changes a record's length.** Each component's glyph ID offset is right, so P1.2 can rewrite it. Pinned in Task 5, and on real hinted data with Noto Sans "Ä".

---

### Task 1: Noto Sans, its licence and the notices

**Files:**
- Create: `src/Maviray.Pdf.Fonts.Default/Fonts/NotoSans-Regular.ttf`, `NotoSans-Bold.ttf`, `NotoSans-Italic.ttf`, `NotoSans-BoldItalic.ttf`, `OFL.txt`
- Create: `THIRD-PARTY-NOTICES.md`
- Test: `tests/Maviray.Pdf.Tests/Fonts/TestFonts.cs`, `tests/Maviray.Pdf.Tests/Fonts/TestFontsTests.cs`

**Source.** Release `NotoSans-v2.015` of `notofonts/latin-greek-cyrillic` (2024-11-20), asset `NotoSans-v2.015.zip` (SHA-256 `0c34df072a3fa7efbb7cbf34950e1f971a4447cffe365d3a359e2d4089b958f5`). The fonts are its `NotoSans/hinted/ttf/` files, and `OFL.txt` is at the zip's root.

| File | Bytes | SHA-256 |
| --- | --- | --- |
| `NotoSans-Regular.ttf` | 621,572 | `478c558ea716033cd60c03438f628dfa75694dcf6b5f6d505a2f05fd2b4f3823` |
| `NotoSans-Bold.ttf` | 631,484 | `1df075a380fc7cb898acf64c1f7b3b4dd780de3caa860178bf929de35817a913` |
| `NotoSans-Italic.ttf` | 639,124 | `467e3f89eeca4108bb8710a2b9e0cf2281ac56d5b0609211a83776d0505eecb5` |
| `NotoSans-BoldItalic.ttf` | 646,092 | `1b602a9d6353be42c91df097a4857b69fa2696f26703d7a33b54a15d87c2622c` |
| `OFL.txt` | 4,396 | `cee9892f9f0cc8fe882c9e9537ee6a89621d86ee7ceaf70b02e2b2b1c25c061a` |

**Interfaces:**
- Produces `internal static class TestFonts` (tests):
  - `static string NotoSansPath(string style)`: `style` is `Regular`, `Bold`, `Italic` or `BoldItalic`. Searches upward from `AppContext.BaseDirectory` for `src/Maviray.Pdf.Fonts.Default/Fonts/NotoSans-<style>.ttf`, and throws `FileNotFoundException` naming that path when no parent has it.
  - `static TrueTypeFont NotoSans(string style)`: loaded once per style and shared (Task 4 adds it; the font is immutable).
  - `static TheoryData<string> Styles`: the four styles.

- [ ] **Step 1: Failing test.** `NotoSansFilesAreTheRecordedRelease`: a theory over the five files. Each file's SHA-256, in lowercase hex, equals the table above.
- [ ] **Step 2:** Verify it fails. Expected: `FileNotFoundException` naming `src/Maviray.Pdf.Fonts.Default/Fonts/`.
- [ ] **Step 3:** Copy the five files from the extracted release. Write `THIRD-PARTY-NOTICES.md`: one section, "Noto Sans", giving the version (2.015), the four file names and their folder, "Copyright 2022 The Noto Project Authors (https://github.com/notofonts/latin-greek-cyrillic)", the SIL Open Font License 1.1 with a link to `OFL.txt` next to the fonts, and one sentence saying that these files are under that licence, not the Maviray.Pdf licence (§6).
- [ ] **Step 4:** Verify it passes. Run `git check-attr binary -- src/Maviray.Pdf.Fonts.Default/Fonts/NotoSans-Regular.ttf`. Expected: `binary: set`.

### Task 2: Test font builder, table reader and table directory

**Files:**
- Create: `src/Maviray.Pdf/Fonts/FontTableReader.cs`, `TableDirectory.cs`
- Test: `tests/Maviray.Pdf.Tests/Fonts/TestFontBuilder.cs`, `TestCmap.cs`, `TestComponent.cs`, `TableDirectoryTests.cs`

**Interfaces:**
- Produces (src):
  - `internal readonly ref struct FontTableReader(ReadOnlySpan<byte> data, string tag)`: `int Length`, `ushort UInt16(int offset)`, `short Int16(int offset)`, `uint UInt32(int offset)`, `int Int32(int offset)`, `ReadOnlySpan<byte> Slice(int offset, int length)`, and `InvalidDataException Error(string problem)`. Every read past the end throws `Error("is too short: …")`, with the needed and the actual length. All values are big-endian.
  - `internal sealed class TableDirectory`
    - `static TableDirectory Read(ReadOnlySpan<byte> file)`
    - `uint SfntVersion`, `bool HasTable(string tag)`, `ReadOnlySpan<byte> GetTable(ReadOnlySpan<byte> file, string tag)` (empty when absent), `IReadOnlyList<string> Tags` (in file order).
    - Rules: a file shorter than 4 bytes is invalid. `ttcf` throws `NotSupportedException` (contains "collection"). `wOFF` and `wOF2` throw `NotSupportedException` (contains "web font"). Versions `0x00010000`, `true` and `OTTO` are read, and anything else is invalid. The 12-byte header and the 16-byte records must fit. A tag listed twice is invalid. A table must lie inside the file. Checksums aren't verified.
- Produces (tests):
  - `TestFontBuilder`, building a complete, valid font by default: glyph 0 `.notdef` (advance 500, box 50,0 to 450,700), glyph 1 empty (advance 250), glyph 2 (advance 600, box 0,0 to 600,700), glyph 3 (advance 650, box 0,0 to 650,700), and the map `U+0020→1`, `A→2`, `B→3`. Tables: `head` (unitsPerEm 1000, long `loca`), `hhea` (800, −200, 0), `maxp` 1.0, `hmtx`, `loca`, `glyf`, a (3, 1) format 4 `cmap`, `OS/2` version 4 (weight 400, fsSelection `0x40`, sCapHeight 700, typo 800/−200/0, win 900/250), `post` format 3, and `name` with PostScript name `TestFont-Regular`, family `Test Font` and subfamily `Regular` as (3, 1, 0x409) records.
  - Its methods, each returning the builder: `SfntVersion(uint)`, `UnitsPerEm(ushort)`, `IndexToLocFormat(short)`, `LineMetrics(short ascender, short descender, short lineGap)`, `NumberOfHMetrics(ushort)`, `ClearGlyphs()`, `SimpleGlyph(ushort advance, short xMin, short yMin, short xMax, short yMax)`, `EmptyGlyph(ushort advance)`, `CompositeGlyph(ushort advance, params TestComponent[] components)`, `Map(int codePoint, ushort glyphId)`, `CmapFormat12()` (writes the map as (3, 10) format 12 instead), `Cmap(byte[] table)` (a whole `cmap` table, from `TestCmap`), `FsType(ushort)`, `Os2(ushort version, ushort weightClass, ushort fsSelection)`, `MacStyle(ushort)`, `Post(int italicAngleFixed, uint isFixedPitch)`, `Names(string? postScript, string? family, string? subfamily)`, `Table(string tag, byte[] data)` (adds or replaces any table, raw), `Without(string tag)`, and `byte[] Build()`. `Build` writes tables in tag order, 4-byte aligned, with correct checksums and `checkSumAdjustment`.
  - `TestComponent(ushort GlyphId, ushort Flags, short Arg1 = 0, short Arg2 = 0)`: the builder adds the bytes each flag needs (`0x0001` word args; then the first of `0x0008` one F2Dot14, `0x0040` two, `0x0080` four), sets `0x0020` on every record but the last, and writes 2 instruction bytes after the last record when its flags have `0x0100`.
  - `TestCmap`: `static byte[] Table(params (ushort Platform, ushort Encoding, byte[] Subtable)[] subtables)`, `static byte[] Format4(params TestSegment[] segments)` (adds the `0xFFFF` segment unless `terminal: false`), `static byte[] Format12(params (uint Start, uint End, uint StartGlyph)[] groups)`, `static byte[] Format(ushort format)` (a subtable of another format, for selection tests). `TestSegment(ushort Start, ushort End, short Delta, ushort[]? Glyphs = null, ushort? RangeOffset = null)`: with `Glyphs`, `idRangeOffset` points at them in the glyph array; `RangeOffset` overrides the computed value.

- [ ] **Step 1: Failing tests** (`TableDirectoryTests`):
  - `ReadsTheTablesOfABuiltFont`: the default font's `Tags` are its 10 tables in byte order of their tags (`OS/2`, `cmap`, `glyf`, `head`, `hhea`, `hmtx`, `loca`, `maxp`, `name`, `post`), and `GetTable(file, "head")` is 54 bytes long.
  - `RefusesACollection` (`ttcf`) and `RefusesWebFonts` (theory over `wOFF`, `wOF2`): `NotSupportedException` with the fragment above.
  - `RefusesAnUnknownVersion` (`0x12345678`) and `RefusesAFileShorterThanAHeader` (0, 3 and 11 bytes): `InvalidDataException`.
  - `RefusesRecordsPastTheEnd`: `numTables` raised to 200.
  - `RefusesATableOutsideTheFile`: theory over an offset of `0xFFFFFFF0` and a length of `0xFFFFFFF0` for `glyf`. The message contains `'glyf'`.
  - `RefusesATagListedTwice`.
- [ ] **Step 2:** Verify they fail. Expected: CS0246 (`TableDirectory` doesn't exist).
- [ ] **Step 3:** Implement the reader and the directory. The builder and `TestCmap` are test code; write them in this step too.
- [ ] **Step 4:** Verify they pass, and run the whole suite.

### Task 3: Character map

**Files:**
- Create: `src/Maviray.Pdf/Fonts/CharacterMap.cs`, `Format4CharacterMap.cs`, `Format12CharacterMap.cs`
- Test: `tests/Maviray.Pdf.Tests/Fonts/CharacterMapTests.cs`

**Interfaces:**
- Consumes: `FontTableReader`.
- Produces `internal abstract class CharacterMap`:
  - `static CharacterMap Parse(ReadOnlySpan<byte> cmapTable, int glyphCount)`: the first of (3, 10) format 12, (0, 4) format 12, (3, 1) format 4, (0, 0) to (0, 3) format 4 (F-04). None of them: `NotSupportedException` containing "Unicode".
  - `static CharacterMap? ParseSubtable(ReadOnlySpan<byte> cmapTable, ushort platformId, ushort encodingId, int glyphCount)`: that encoding's subtable in format 4 or 12, or null.
  - `abstract ushort GetGlyphId(int codePoint)`, and `abstract IEnumerable<(int CodePoint, ushort GlyphId)> Mappings()`, in ascending code point order, without glyph 0.
- A subtable's bytes run from its offset to the end of the `cmap` table: the format 4 `length` field isn't trusted. Every check runs at parse, so a lookup never reads outside the subtable.

- [ ] **Step 1: Failing tests:**
  - Format 4:
    - `Format4MapsARangeByDelta`: segment `0x41`–`0x43` with delta −63 gives `A→2`, `B→3`, `C→4`.
    - `Format4DeltaWrapsModulo65536`: segment `0xFFF0`–`0xFFF0` with delta 20 gives glyph 4.
    - `Format4MapsThroughTheGlyphArray`: segment `0x61`–`0x63` with glyphs `[5, 0, 6]` and delta 1 gives `a→6`, `b→0`, `c→7`.
    - `Format4RefusesARangeOffsetPastTheSubtable`: a `RangeOffset` of 2000.
    - `Format4RefusesAMissingFinalSegment` (`terminal: false`), and `Format4RefusesSegmentsOutOfOrder`.
    - `Format4CannotMapAboveTheBmp`: U+1F600 gives 0.
  - Format 12:
    - `Format12MapsGroups`: `(0x41, 0x43, 2)` and `(0x1F600, 0x1F600, 5)` map `C→4` and `U+1F600→5`.
    - `Format12RefusesOverlappingGroups` and `Format12RefusesUnsortedGroups`.
    - `Format12ReturnsZeroForSurrogatesAndBeyondUnicode`: a group `(0xD800, 0x10FFFF, 1)` gives 0 for U+D800, U+DFFF and `0x110000`.
  - Both formats:
    - `GlyphsPastTheFontAreUnmapped`: a mapping to glyph 99 in a font of 4 glyphs gives 0, and `Mappings()` leaves it out.
    - `MappingsAreAscendingWithoutNotdef`.
  - Selection:
    - `PrefersFormat12OverFormat4`: (3, 1) maps `A→2`, (3, 10) maps `A→3`. The result is 3.
    - `FallsBackToTheUnicodePlatform`: only (0, 3) format 4.
    - `IgnoresAnEncodingInAnotherFormat`: (3, 1) in format 6 (`TestCmap.Format(6)`) and (0, 3) format 4. (0, 3) is used.
    - `RefusesASymbolOnlyCmap`: only (3, 0) format 4. `NotSupportedException`.
  - Real data, `NotoSansFormat4AndFormat12Agree`: a theory over the four styles. The `cmap` comes from `TableDirectory.Read(file).GetTable(file, "cmap")` over the file's bytes, because `TrueTypeFont` arrives in Task 4. With a glyph count of 65535 for both, the (3, 1) and (3, 10) subtables give the same glyph for every code point from 0 to `0xFFFF`.
- [ ] **Step 2:** Verify they fail. Expected: CS0246.
- [ ] **Step 3:** Implement. Format 4 keeps its four arrays and searches end codes with a binary search. Format 12 keeps its groups as arrays of start, end and start glyph.
- [ ] **Step 4:** Verify they pass, and run the whole suite.

### Task 4: `TrueTypeFont`

**Files:**
- Create in `src/Maviray.Pdf/Fonts/`: `TrueTypeFont.cs`, `HeaderTable.cs` (`head`), `HorizontalHeaderTable.cs` (`hhea`), `MaximumProfileTable.cs` (`maxp`), `HorizontalMetricsTable.cs` (`hmtx`), `GlyphLocationTable.cs` (`loca`), `Os2Table.cs`, `PostTable.cs`, `NamingTable.cs`, `EmbeddingPermissions.cs`, `FontEmbedding.cs`, `LineMetrics.cs`, `GlyphBounds.cs`, `GlyphHeader.cs`
- Test: `tests/Maviray.Pdf.Tests/Fonts/TrueTypeFontTests.cs`, `EmbeddingPermissionsTests.cs`; add `TestFonts.NotoSans`

**Interfaces:**
- Consumes: `TableDirectory`, `CharacterMap`, `FontTableReader`.
- Produces:
  - `enum FontEmbedding { Subset, Whole }`
  - `readonly record struct LineMetrics(int Ascender, int Descender, int LineGap)`, `readonly record struct GlyphBounds(short XMin, short YMin, short XMax, short YMax)`, `readonly record struct GlyphHeader(short ContourCount, GlyphBounds Bounds)` with `bool IsComposite => ContourCount < 0`.
  - `static class EmbeddingPermissions`: `static FontEmbedding Resolve(ushort fsType)`.
  - `sealed class TrueTypeFont`:
    - `static TrueTypeFont Load(ReadOnlySpan<byte> file)`: copies the bytes first.
    - Names: `string? PostScriptName`, `string? FamilyName`, `string? SubfamilyName`.
    - Header values: `int UnitsPerEm`, `int GlyphCount`, `GlyphBounds FontBounds`, `ushort MacStyle`.
    - Line metrics: `LineMetrics HorizontalHeaderMetrics`, `LineMetrics? TypographicMetrics`, `LineMetrics? WindowsMetrics`. Windows metrics are `(usWinAscent, −usWinDescent, 0)`.
    - Style and embedding: `ushort FsType`, `FontEmbedding Embedding`, `int WeightClass`, `bool IsItalic`, `bool IsFixedPitch`, `double ItalicAngle`, `int? CapHeight`, `int UnderlinePosition`, `int UnderlineThickness`.
    - Lookups: `ushort GetGlyphId(int codePoint)`, `IEnumerable<(int CodePoint, ushort GlyphId)> CharacterMappings()`, `int GetAdvanceWidth(ushort glyphId)`, `ReadOnlySpan<byte> GetGlyphData(ushort glyphId)`, `GlyphHeader? GetGlyphHeader(ushort glyphId)` (null for an empty glyph; data shorter than 10 bytes throws `InvalidDataException`), and `bool TryGetTable(string tag, out ReadOnlySpan<byte> table)`.
- **Load order:**
  1. The directory.
  2. `name`, when present.
  3. Refusals, in this order: `OTTO` (contains "CFF"), `fvar` (contains "variable"), then `COLR`, `CBDT`, `sbix` or `SVG ` (contains "colour").
  4. The required tables `head`, `hhea`, `maxp`, `hmtx`, `cmap`, `loca` and `glyf`. A missing one is invalid, and the message names it.
  5. `head`, `maxp`, `hhea`, `hmtx`, `loca`, `cmap`, `OS/2` with `EmbeddingPermissions`, and `post`.
- **Table rules:**
  - `head` needs 54 bytes, `unitsPerEm` from 16 to 16384, and `indexToLocFormat` of 0 or 1.
  - `hhea` needs 36 bytes, and `numberOfHMetrics` from 1 to `numGlyphs`.
  - `maxp` needs 6 bytes, and `numGlyphs` of at least 1.
  - `hmtx` needs `4 × numberOfHMetrics + 2 × (numGlyphs − numberOfHMetrics)` bytes.
  - `loca` needs `numGlyphs + 1` entries. Short entries are doubled. Entries never decrease, and the last is at most the length of `glyf`.
  - `OS/2` needs 78 bytes for version 0, 86 for version 1, 96 for versions 2 to 4, and 100 from version 5. `sCapHeight` is read from version 2.
  - `post` needs 32 bytes. `italicAngle` is a 16.16 fixed-point number.
  - `name` records and strings must lie inside the table. Windows Unicode ((3, 1) or (3, 10)) strings are UTF-16BE, and an odd length is invalid. The order is: Windows Unicode in language `0x409`, then any Windows Unicode, then platform 0, then (1, 0) language 0. Macintosh strings are read as ASCII, with `?` for bytes from `0x80`. An empty string counts as absent.
- **Without `OS/2`:** `FsType` is 0, `WeightClass` is 700 when `macStyle` bit 0 is set and 400 otherwise, and the typographic and Windows metrics are null. `IsItalic` is `fsSelection` bit 0 or `macStyle` bit 1. Without `post`, the angle, pitch and underline values are 0.

- [ ] **Step 1: Failing tests:**
  - `EmbeddingPermissionsTests.Resolves`: a theory.
    - `Subset`: `0x0000`, `0x0004`, `0x0008`, `0x0006` and `0x000A`.
    - `Whole`: `0x0100` and `0x0108`.
    - `NotSupportedException` containing "Restricted License": `0x0002` and `0x0102`.
    - `NotSupportedException` containing "Bitmap embedding only": `0x0200`, `0x0204` and `0x0208`.
  - `TrueTypeFontTests`:
    - `LoadsTheBuiltFont`: every property of the default font, and the advances 500, 250, 600 and 650.
    - `AdvancesPastNumberOfHMetricsRepeatTheLast`: with `NumberOfHMetrics(2)`, glyphs 2 and 3 have advance 250.
    - `GlyphDataAndHeaders`: glyph 1 has empty data and a null header. Glyph 2's header is `(1, (0, 0, 600, 700))`.
    - `ShortLocaIsDoubled`: `IndexToLocFormat(0)` gives the same glyph data.
    - Refusals: `RefusesCff`, `RefusesVariableFonts`, `RefusesColourFonts` (theory over the four tags) and `RefusesRestrictedFonts` (`fsType 0x0002`). Each message starts with `TestFont-Regular: `.
    - `RefusesAMissingRequiredTable`: a theory over the seven tags.
    - `RefusesBadValues`: a theory, one case per rule above. `unitsPerEm` 15 and 16385, `indexToLocFormat` 2, `numberOfHMetrics` 0 and 5, `numGlyphs` 0, a short `hmtx`, a decreasing `loca`, a `loca` past `glyf`, OS/2 version 2 with 86 bytes, a 20-byte `post`, a `name` string past the table. Each is `InvalidDataException` naming its table.
    - `WithoutOs2TheStyleComesFromMacStyle`: `Without("OS/2")` with `MacStyle(3)` gives weight 700, italic, `FsType` 0, and null typographic metrics.
    - `NameFallsBackToMacintosh`: only a (1, 0) PostScript name record.
    - `LoadKeepsItsOwnCopy`: the source array is cleared after `Load`, and the lookups are unchanged.
    - `NotoSansStyles`: a theory over the four styles.
      - Every style: `UnitsPerEm` 1000, `HorizontalHeaderMetrics` (1069, −293, 0), `CapHeight` 714, `FsType` 0 and `Embedding` `Subset`.
      - The rest per style:

        | Style | `GlyphCount` | `WeightClass` | `IsItalic` | `ItalicAngle` | `PostScriptName` |
        | --- | --- | --- | --- | --- | --- |
        | Regular | 3884 | 400 | false | 0 | `NotoSans-Regular` |
        | Bold | 3884 | 700 | false | 0 | `NotoSans-Bold` |
        | Italic | 3895 | 400 | true | −12 | `NotoSans-Italic` |
        | BoldItalic | 3895 | 700 | true | −12 | `NotoSans-BoldItalic` |

      - `FamilyName` is `Noto Sans`.
      - `A`, `Ä`, `İ`, `ı`, `Ə`, `Ґ`, `Ω` and `€` map to glyphs other than 0. U+4E00 maps to 0.
      - These values were read from the files with a throwaway script while planning. Task 7's fontTools check is the independent confirmation.
- [ ] **Step 2:** Verify they fail. Expected: CS0246.
- [ ] **Step 3:** Implement. Each table type has a `static Parse(ReadOnlySpan<byte> table, …)` that returns its values. `Load` wraps everything after the `name` table in one `try`, and rethrows `InvalidDataException` and `NotSupportedException` with the `<PostScriptName>: ` prefix and the original as the inner exception.
- [ ] **Step 4:** Verify they pass, and run the whole suite.

### Task 5: Composite components

**Files:**
- Create: `src/Maviray.Pdf/Fonts/CompositeGlyph.cs`, `GlyphComponent.cs`
- Test: `tests/Maviray.Pdf.Tests/Fonts/CompositeGlyphTests.cs`

**Interfaces:**
- Produces:
  - `readonly record struct GlyphComponent(ushort GlyphId, int GlyphIdOffset)`. `GlyphIdOffset` is the position of the component's glyph ID within the glyph's data, which is where P1.2 rewrites it.
  - `static class CompositeGlyph`: `static GlyphComponent[] ReadComponents(ReadOnlySpan<byte> glyphData, int glyphCount)`. It returns an empty array for an empty or simple glyph. A truncated record, or a component ID of `glyphCount` or more, throws `InvalidDataException` naming `'glyf'`. Cycles and depth are P1.2's closure rules.

- [ ] **Step 1: Failing tests:**
  - `ReadsOneComponentWithByteArguments`: flags `0x0000`. The result is `[(2, 12)]`.
  - `RecordLengthFollowsTheFlags`: a theory over two components, each with flags `f`, where the second ID's offset is 12 + the length of the first record. Word arguments (`0x0001`) give 20, one scale (`0x0008`) 20, X and Y scales (`0x0040`) 22, and a two-by-two matrix (`0x0080`) 26. With all four set, the single scale wins, in the order the OpenType specification's pseudo-code tests them (`0x0008`, then `0x0040`, then `0x0080`): 22.
  - `InstructionsAfterTheLastRecordAreIgnored`: flags `0x0100` on the last record.
  - `RefusesATruncatedRecord` and `RefusesAComponentPastTheFont`.
  - `SimpleAndEmptyGlyphsHaveNoComponents`.
  - `NotoSansADieresisIsAPlusAMark`: a theory over the four styles. `Ä` has two components, and the first is the glyph of `A`. Its second record has instructions (`0x0107`), so this covers real hinted data.
- [ ] **Step 2:** Verify they fail. Expected: CS0103.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Verify they pass, and run the whole suite.

### Task 6: Arbitrary bytes

**Files:** test `tests/Maviray.Pdf.Tests/Fonts/DamagedFontTests.cs`

- [ ] **Step 1: Tests.** `Exercise(TrueTypeFont font)` is a helper. It calls:
  - `GetGlyphId` for every code point from 0 to `0x2FF`, and for `0x1F600`;
  - `CharacterMappings().Take(100_000).Count()`;
  - for every glyph, `GetAdvanceWidth`, `GetGlyphHeader`, and `CompositeGlyph.ReadComponents` on its data.

  The tests use the default built font with one composite glyph added (components 2 and 3, flags `0x0001` and `0x0008`):
  - `EveryTruncationThrowsInvalidData`: every length from 0 to the end of the last table's data, minus 1, throws `InvalidDataException`. Longer lengths load.
  - `EveryByteChangeLoadsOrThrowsTheDocumentedExceptions`: for every offset, and the values `0x00`, `0x7F`, `0x80` and `0xFF`, either `Load` and `Exercise` succeed, or they throw `InvalidDataException` or `NotSupportedException`. The failure message names the offset and the value of the first change that throws anything else.
- [ ] **Step 2:** Run them. A test here can't fail before code exists, so its red step is a mutant: remove the `loca` bounds check from Task 4, watch `EveryByteChangeLoadsOrThrowsTheDocumentedExceptions` fail with an offset in `loca`, and restore the check.
- [ ] **Step 3:** Fix every escape the tests find in the owning type. Each fix stays covered by these two tests.
- [ ] **Step 4:** Verify they pass, and run the whole suite.

### Task 7: fontTools as a validator, in CI

**Files:**
- Create: `tests/Maviray.Pdf.Tests/Validation/fonttools_check.py`, `FontTools.cs`, `FontDump.cs`; test `tests/Maviray.Pdf.Tests/Fonts/FontToolsAgreementTests.cs`
- Modify: `tests/Maviray.Pdf.Tests/Validation/ExternalTool.cs`, `ValidatorSelfTests.cs`, `tests/Maviray.Pdf.Tests/Maviray.Pdf.Tests.csproj` (copy `Validation/*.py` to the output, `PreserveNewest`), `.github/workflows/build.yml`, `.github/workflows/publish-nuget.yml`, `CONTRIBUTING.md`

**Interfaces:**
- `ExternalTool`:
  - `Locate(string tool, string installHint)`. The existing `Locate(string tool)` calls it with the qpdf and poppler hint.
  - `static void Unavailable(string message)`: skips, or fails when `MAVIRAY_PDF_REQUIRE_VALIDATORS` is `true`. `Locate` uses it too.
- `FontTools`:
  - `static string Python()`: the first of `python3` and `python` for which `<it> -I -c "import fontTools"` exits with 0. Otherwise `Unavailable("fontTools is not installed. Install it with 'pip install fonttools' or the python3-fonttools package to run this check.")`.
  - `static FontDump Dump(string fontPath)`: runs `fonttools_check.py dump <fontPath>` with `-I`. A non-zero exit fails the test, with the script's output.
- `FontDump`: `int UnitsPerEm`, `int NumGlyphs`, `int Ascender`, `int Descender`, `int FsType`, and `IReadOnlyDictionary<int, (int GlyphId, int Advance)> Cmap`. It is parsed from the script's JSON with `JsonDocument`.
- The script's output: `{"unitsPerEm": …, "numGlyphs": …, "ascender": …, "descender": …, "fsType": …, "cmap": {"<code point>": [<glyph id>, <advance>], …}}`. It comes from `TTFont(path)`, `getBestCmap()`, `getGlyphID()` and `hmtx`, with `ensure_ascii` output. It skips code points mapped to glyph 0. On any exception, it prints the error to stderr and exits with 1.

- [ ] **Step 1: Failing tests:**
  - `FontToolsAgreementTests.OurParserAgreesWithFontTools`: a theory over the four Noto Sans styles. The five numbers are equal. The cmap from `CharacterMappings()`, with `GetAdvanceWidth`, equals the dump's, both ways.
  - `ValidatorSelfTests.FontToolsRejectsAFileThatIsNotAFont`: `FontTools.Python()` first, then `Dump` on a saved file of 100 zero bytes throws `XunitException`.
- [ ] **Step 2:** Verify they fail. Expected: CS0103 (`FontTools` doesn't exist).
- [ ] **Step 3:** Implement the helpers, the script and the project item. Locally, without fontTools, both tests skip with the message. The PR's CI run is where they first run.
- [ ] **Step 4: CI.** In both workflows:
  - add `python3-fonttools` to the `apt-get install` line;
  - add `python3 -c "import fontTools; print('fontTools', fontTools.version)"` after `pdftoppm -v`;
  - rename the step "Install the PDF and font validators".

  In `CONTRIBUTING.md`, under "Running the tests":
  - name fontTools beside qpdf and poppler, for checking fonts;
  - add `pip install fonttools`, or `python3-fonttools` on Debian and Ubuntu, to the install lines;
  - say that the check needs `python3` or `python` on `PATH`.
- [ ] **Step 5:** Verify the whole suite passes locally, with the fontTools tests skipped.

### Task 8: Records

- [ ] Write `docs/releases/0.1.0/execution-logs/2026-11-20-issue-20-truetype-parser.md`, and set #20's row in the release index to "In review" with the plan and the log. Commit the three commits (fonts and notices, code, records), push `feature/20-truetype-parser`, open the PR into `develop` with `Refs #20`, and record the first CI run in the log: tests run, and 0 skipped.
