# P1.5 Unicode data and NFC: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The UCD 16.0.0 files in the repository, Unicode tables generated from them, and `TextNormalizer.ToNfc`, an NFC of our own that replaces unpaired surrogates, returns NFC text as the same instance, and passes every line of `NormalizationTest.txt` in both globalization modes.

**Architecture:** The compressed UCD files live in the test project. A generator there builds a model of the normalization data (combining classes, full decompositions, composition exclusions, primary composites, the quick check) and writes it as three C# data classes into `src/Maviray.Pdf/Text/Unicode/`. Handwritten lookups in `src/Maviray.Pdf/Text/` read those tables, and `TextNormalizer` runs the Unicode Standard's algorithm on them, behind a quick check. A test regenerates the tables and fails if the committed ones differ.

**Tech Stack:** .NET 10, C#, xUnit v3 on MTP, `System.IO.Compression.GZipStream` in the tests, GNU gzip once to compress the files.

**Spec:** [P1 Text](../specs/2026-11-23-p1-text-design.md), decisions T-01 to T-04. Work item [#30](https://github.com/Maviray/pdf/issues/30).

## Global Constraints

- Every type in `src/` is `internal`: handwritten code in `src/Maviray.Pdf/Text/` (namespace `Maviray.Pdf.Text`), generated tables only in `src/Maviray.Pdf/Text/Unicode/` (namespace `Maviray.Pdf.Text.Unicode`). One type per file, no partial classes, no reflection, no package references in `src/`, warnings as errors, trimming- and AOT-safe.
- `src/` never reads a file: the tables are `static ReadOnlySpan<T>` properties over constant arrays (`=> new ushort[] { … }`), so nothing is allocated or parsed at start-up.
- No ICU and no culture in `src/Maviray.Pdf/Text/`, and no call to `Normalize` or `IsNormalized` anywhere in `src/` (T-01). Our own entry point is therefore named `ToNfc`.
- Expected values come from the UCD files, the Unicode Standard or UAX #15. A value our own code computes is never the expectation for itself. The one exception is labelled: Task 3 compares the committed tables with the generator's model, which checks the encoding, not the data.
- C# that contains backslashes is written with the file tools, never through a shell here-document (P0.1 log).
- Generated files are UTF-8 without a BOM, with LF line endings, and byte-for-byte reproducible.
- The work is committed as three commits: the data and notices, the code, and the records.

## Decisions this plan makes

- **All eight UCD files arrive now** (T-02 fetches them once), but only the normalization tables are generated. P1.6 adds the line-break tables, and P1.7 `Default_Ignorable_Code_Point` and the general category, to the same generator.
- **`NormalizationTest.txt` 16.0.0 has six parts**, `@Part0` to `@Part5`, 19,965 lines. T-04 says "four parts"; every line of every part is checked, and the spec's wording is corrected in Task 6.
- **One entry point does T-03 step 1 and T-04:** `TextNormalizer.ToNfc` replaces unpaired surrogates while it normalizes. The quick check treats a lone surrogate as a change.
- **Handwritten lookups live in `Text/`, generated data alone in `Text/Unicode/`,** so the staleness test can require that folder to hold exactly the generated files.

## Data

Downloaded on 2026-11-23 from `https://www.unicode.org/Public/16.0.0/ucd/` (and `https://www.unicode.org/license.txt`), all with LF line endings. The checksums are of the uncompressed files.

| File | Bytes | SHA-256 |
| --- | --- | --- |
| `UnicodeData.txt` | 2,175,362 | `ff58e5823bd095166564a006e47d111130813dcf8bf234ef79fa51a870edb48f` |
| `CompositionExclusions.txt` | 9,007 | `89e83cf9cc8bef6c1f8bf77e42cf6f0341dfa42e66261f4dbe9b492e7a23c8ee` |
| `DerivedCoreProperties.txt` | 1,115,959 | `39d35161f2954497f69e08bdb9e701493f476a3d30222de20028feda36c1dabd` |
| `LineBreak.txt` | 260,441 | `e97e4259d0d20fab150b9c7b4b28abfae5cd78ca97e7f4ac6ed20d685d5f4a7c` |
| `EastAsianWidth.txt` | 199,042 | `43adc76c0686a42cb370764eb8cfe2b2a45b10b855e5572a2db4a0eecce15d5b` |
| `emoji-data.txt` (from `emoji/`) | 113,024 | `f1365a5173eee18e1f98b240cdc492e84a25f1ce7e0c9d1094eb29c41a22696a` |
| `NormalizationTest.txt` | 2,798,671 | `d811971453e7075e1ad56fb1b301eece5aa80757b81f6156e74a1bfb3ae5ceb1` |
| `LineBreakTest.txt` (from `auxiliary/`) | 3,037,870 | `910759a611a479f37df4f535d2e64d7589be3c5ac5f7491cf1fb4fa4cdb211e9` |
| `license.txt` | 1,995 | `e7a93b009565cfce55919a381437ac4db883e9da2126fa28b91d12732bc53d96` |

Values read from these files with a throwaway script, independent of the code below, which the tests pin:
- 2,081 code points have a canonical decomposition. The full composition exclusions number 1,120: 81 listed in `CompositionExclusions.txt`, 1,035 singletons, and 7 non-starter decompositions. That leaves 961 primary composites.
- The longest full decomposition is 4 code points (U+1F87 → U+03B1 U+0314 U+0342 U+0345).
- 132 code points are `NFC_Quick_Check` Maybe: 120 that can join the character before them, 48 of them the Hangul V and T jamo, and 12 chained composites new in 16.0 (U+113C5, U+113C7, U+113C8, U+16121 to U+16128 and U+16D68), whose decomposition starts with one of those 120. The other 1,120 non-Yes code points are No. Nothing below U+0300 has a combining class or a quick-check value other than Yes.
  - *Corrected during Task 4.* The first count, 120, came from a script that missed the chained composites. `NormalizationTest.txt` part 5 caught it, and the execution log records it.

## Review Focus

1. **NFC text with characters that may combine backwards**, such as `q` U+0308, U+0915 U+093C and a leading U+0301. It comes back as the same instance, with nothing allocated. Pinned in Task 4.
2. **Unpaired surrogates anywhere**: first, last, reversed pairs, and between a base letter and its mark. Each becomes U+FFFD, which is a starter, so it blocks the composition across it. Pinned in Task 4.
3. **A long run of combining marks of mixed classes**, which an insertion sort would make quadratic. Pinned in Task 4, with a time limit.
4. **Code points beyond the BMP**: Kaithi U+11099 U+110BA composes to U+1109A, and U+1D15E stays decomposed. Pinned in Task 4, and for every code point in Task 3.
5. **The first change deep in a long text, or at index 0 with no starter before it.** The text before the last starter is kept as it is. Pinned in Task 4.

---

### Task 1: The Unicode data, its notice and the packages

**Files:**
- Create: `tests/Maviray.Pdf.Tests/Text/Unicode/Data/16.0.0/` with the eight files as `<name>.txt.gz`, and `license.txt`
- Modify: `.gitattributes`, `THIRD-PARTY-NOTICES.md`, `src/Directory.Build.props`
- Test: `tests/Maviray.Pdf.Tests/Text/Unicode/UcdFiles.cs`, `UcdFilesTests.cs`, `UnicodeNoticeTests.cs`

**Interfaces:**
- Produces `internal static class UcdFiles` (tests):
  - `const string Version = "16.0.0"`;
  - `static string DataFolder`: searches upward from `AppContext.BaseDirectory` for `tests/Maviray.Pdf.Tests/Text/Unicode/Data/16.0.0`, and throws `DirectoryNotFoundException` naming it;
  - `static string ReadText(string fileName)`: the decompressed UTF-8 text of `<fileName>.gz`, for example `ReadText("UnicodeData.txt")`; `license.txt` is read as it is;
  - `static IEnumerable<(int LineNumber, string[] Fields)> ReadRecords(string fileName)`: each line with its `#` comment removed, skipping blank lines, split at `;` and trimmed.

- [ ] **Step 1: Failing test.** `UcdFilesTests.FilesAreTheRecordedRelease`: a theory over the nine files of the table above. Each one's text, as UTF-8 bytes, has the recorded SHA-256 in lowercase hex.
- [ ] **Step 2:** Verify it fails. Expected: `DirectoryNotFoundException` naming `tests/Maviray.Pdf.Tests/Text/Unicode/Data/16.0.0`.
- [ ] **Step 3:** Compress each downloaded file with `gzip -n -9 -c <name>.txt > <name>.txt.gz` (`-n` leaves out the name and time, so the bytes are reproducible) into the data folder, and copy `license.txt` unchanged. Add to `.gitattributes`: `*.gz binary`, and `tests/Maviray.Pdf.Tests/Text/Unicode/Data/16.0.0/license.txt text eol=lf` with a comment that its checksum is pinned.
- [ ] **Step 4:** Verify it passes. Run `git check-attr binary eol -- tests/Maviray.Pdf.Tests/Text/Unicode/Data/16.0.0/UnicodeData.txt.gz tests/Maviray.Pdf.Tests/Text/Unicode/Data/16.0.0/license.txt`. Expected: `binary: set` for the first, `eol: lf` for the second.
- [ ] **Step 5: Failing tests** (`UnicodeNoticeTests`):
  - `NoticesCarryTheUnicodeLicence`: `THIRD-PARTY-NOTICES.md` contains a `## Unicode Character Database` section, and the whole of `license.txt`, with both sides' line endings made LF first. Write beside the comparison that it can't see line-ending differences.
  - `PackagesShipTheNotices`: `src/Directory.Build.props`, read as XML, has a `None` item whose `Include` ends with `THIRD-PARTY-NOTICES.md`, with `Pack="true"` and `PackagePath="\"`.
- [ ] **Step 6:** Verify both fail: the notices file has no Unicode section, and the props file no such item.
- [ ] **Step 7:** Write the section: the eight files and their folder, version 16.0.0, the source URL, the sentence "The Unicode tables compiled into `Maviray.Pdf.dll` are generated from these files.", and the full licence in a fenced block. Add the `None` item beside `LICENSE` in `src/Directory.Build.props`.
- [ ] **Step 8:** Verify both pass. Then `dotnet pack src/Maviray.Pdf -c Release -o <scratchpad>/pack` and list the `.nupkg`'s entries. Expected: `THIRD-PARTY-NOTICES.md` at the root, beside `LICENSE` and `README.md`.
- [ ] **Step 9: Commit** the data folder, `.gitattributes`, the notices, the props file and the three test files: `#30 add the Unicode 16.0.0 data and its notice`.

### Task 2: The generator and the normalization tables

**Files:**
- Create (tests): `tests/Maviray.Pdf.Tests/Text/Unicode/NormalizationModel.cs`, `CSharpTableWriter.cs`, `UnicodeTableGenerator.cs`, `GeneratedFile.cs`
- Create (generated): `src/Maviray.Pdf/Text/Unicode/NfcPropertyData.cs`, `DecompositionData.cs`, `CompositionData.cs`
- Create (src): `src/Maviray.Pdf/Text/NfcQuickCheck.cs`
- Modify: `.gitattributes`
- Test: `tests/Maviray.Pdf.Tests/Text/Unicode/NormalizationModelTests.cs`, `UnicodeTablesTests.cs`

**Interfaces:**
- Consumes `UcdFiles` (Task 1).
- Produces (tests):
  - `internal sealed class NormalizationModel`, `static NormalizationModel Load()` (built once and shared) from `UnicodeData.txt` and `CompositionExclusions.txt`:
    - `byte CombiningClass(int codePoint)`;
    - `IReadOnlyList<int> FullDecomposition(int codePoint)`: the canonical mapping expanded until stable, empty when there is none. Hangul syllables have none here;
    - `IReadOnlySet<int> FullCompositionExclusions`: the listed exclusions, the singletons, and every decomposition whose character or first code point has a non-zero combining class;
    - `IReadOnlyDictionary<(int First, int Second), int> PrimaryComposites`: every two-code-point canonical mapping whose character isn't excluded;
    - `NfcQuickCheck QuickCheck(int codePoint)`: No for an exclusion; Maybe for the second code point of a primary composite, for U+1161 to U+1175 and U+11A8 to U+11C2, and for a composite whose full decomposition starts with one of those; Yes otherwise.
  - `internal sealed record GeneratedFile(string FileName, string Content)`.
  - `internal static class UnicodeTableGenerator`: `static IReadOnlyList<GeneratedFile> Generate()`, the three files, and `static string TablesFolder`, the absolute path of `src/Maviray.Pdf/Text/Unicode/`, found from `src/Maviray.Pdf/Maviray.Pdf.csproj` upward.
- Produces (src, generated), `internal static class`es in `Maviray.Pdf.Text.Unicode`:
  - `NfcPropertyData`: `const int BlockShift = 7`, `ReadOnlySpan<ushort> BlockIndex` (one entry per 128 code points, 8,704 entries) and `ReadOnlySpan<ushort> Blocks` (the distinct 128-entry blocks). An entry is the combining class in its low byte and the `NfcQuickCheck` value in bits 8 and 9. *Changed during Task 2:* `BlockIndex` holds `byte`s, because 16.0.0 has only 114 distinct blocks (see the execution log).
  - `DecompositionData`: `ReadOnlySpan<int> CodePoints` (sorted, one per decomposable code point), `ReadOnlySpan<ushort> Starts` (one more entry than `CodePoints`) and `ReadOnlySpan<int> Mappings`. The full decomposition of `CodePoints[i]` is `Mappings[Starts[i]..Starts[i + 1]]`.
  - `CompositionData`: `ReadOnlySpan<ulong> Pairs`, sorted, each `(ulong)first << 32 | (uint)second`, and `ReadOnlySpan<int> Composites`, in the same order.
- `NfcQuickCheck` (src, Task 3) is `internal enum NfcQuickCheck : byte { Yes = 0, Maybe = 1, No = 2 }`. Task 2 creates it, because the model returns it.

**Generated file format:** line 1 `// <auto-generated/>`, then comment lines naming the source (`Unicode Character Database 16.0.0, https://www.unicode.org/Public/16.0.0/ucd/`, with the files used), the generator (`tests/Maviray.Pdf.Tests/Text/Unicode/UnicodeTableGenerator.cs`), "Do not edit. Regenerate with MAVIRAY_PDF_WRITE_UNICODE_TABLES=true, as CONTRIBUTING.md describes.", and every line of `license.txt`. Then the namespace and one class. Numbers are written in invariant form, 16 to a line, with code points as `0x` hex. LF line endings, no BOM.

- [ ] **Step 1: Failing tests** (`NormalizationModelTests`), with the values from **Data** above:
  - `CountsMatchTheUcd`: 2,081 decomposable code points, 1,120 full composition exclusions, 961 primary composites, 132 Maybe, 1,120 No, and a longest full decomposition of 4;
  - `CombiningClasses`: U+0301 → 230, U+0323 → 220, U+093C → 7, U+0345 → 240, U+1D165 → 216, U+0041 → 0, unassigned U+0378 → 0;
  - `FullDecompositions`: U+1F87 → U+03B1 U+0314 U+0342 U+0345; U+1E08 → U+0043 U+0327 U+0301; U+212B → U+0041 U+030A; U+1109A → U+11099 U+110BA; U+0041 → empty; U+AC00 → empty;
  - `Exclusions`: U+0958 (listed), U+212B and U+2126 (singletons), U+0344 and U+0F73 (non-starter), U+2ADC (listed), U+1D15E (listed) are excluded; U+00C5 and U+1109A aren't;
  - `Composites`: (U+0041, U+030A) → U+00C5; (U+0078, U+0308) → U+1E8D; (U+0061, U+0323) → U+1EA1; (U+1EA1, U+0302) → U+1EAD; (U+11099, U+110BA) → U+1109A; none for (U+0071, U+0308), (U+0915, U+093C), (U+2ADD, U+0338) and (U+1EA1, U+0301);
  - `QuickCheck`: U+0041 Yes, U+00C5 Yes, U+AC00 Yes, U+0301 Maybe, U+093C Maybe, U+09BE Maybe, U+1161 Maybe, U+11A8 Maybe, U+0958 No, U+212B No, U+0344 No.
- [ ] **Step 2:** Verify they fail: `NormalizationModel` doesn't exist.
- [ ] **Step 3:** Implement `NormalizationModel`. `UnicodeData.txt`'s `First>`/`Last>` ranges have class 0 and no mapping. A field 5 that starts with `<` is a compatibility mapping and is ignored.
- [ ] **Step 4:** Verify they pass.
- [ ] **Step 5: Failing tests** (`UnicodeTablesTests`):
  - `CommittedTablesAreCurrent`: for each generated file, the committed file's bytes equal the content's UTF-8 bytes. When `MAVIRAY_PDF_WRITE_UNICODE_TABLES` is `true`, it writes the files instead, and says so in the test output.
  - `TablesFolderHoldsOnlyGeneratedFiles`: the `.cs` files in `TablesFolder` are exactly the generated names.
  - `GenerationIsDeterministic`: two `Generate()` calls give equal contents.
- [ ] **Step 6:** Verify `CommittedTablesAreCurrent` fails, naming the missing file.
- [ ] **Step 7:** Implement `CSharpTableWriter` and `UnicodeTableGenerator`, and `NfcQuickCheck`. Add to `.gitattributes`: `src/Maviray.Pdf/Text/Unicode/*.cs text eol=lf`, with a comment that the staleness test compares them byte for byte.
- [ ] **Step 8:** Run `MAVIRAY_PDF_WRITE_UNICODE_TABLES=true dotnet test --project tests/Maviray.Pdf.Tests --filter-class "Maviray.Pdf.Tests.Text.Unicode.UnicodeTablesTests"`, then `dotnet build`. Expected: three files written, and the build passes with no warnings. Record their sizes in the log: together they stay within a few tens of kilobytes of data (T-02's "Approaches").
- [ ] **Step 9:** Verify all three tests pass without the variable, and `git check-attr eol -- src/Maviray.Pdf/Text/Unicode/NfcPropertyData.cs` gives `eol: lf`.

### Task 3: Lookups and Hangul

**Files:**
- Create: `src/Maviray.Pdf/Text/NormalizationLookup.cs`, `src/Maviray.Pdf/Text/Hangul.cs`
- Test: `tests/Maviray.Pdf.Tests/Text/NormalizationLookupTests.cs`, `HangulTests.cs`

**Interfaces:**
- Consumes the generated data classes and `NfcQuickCheck` (Task 2).
- Produces (src):
  - `internal static class Hangul`: the constants of the Unicode Standard 3.12 (`SBase` 0xAC00, `LBase` 0x1100, `VBase` 0x1161, `TBase` 0x11A7, `LCount` 19, `VCount` 21, `TCount` 28, `SCount` 11172); `static bool IsSyllable(int codePoint)`; `static int Decompose(int syllable, Span<int> destination)`, which writes 2 or 3 jamo; `static int Compose(int first, int second)`: LV from L and V, LVT from LV and T, otherwise −1.
  - `internal static class NormalizationLookup`:
    - `const int MaxDecompositionLength = 4`;
    - `static int CombiningClass(int codePoint)` and `static NfcQuickCheck QuickCheck(int codePoint)`: 0 and Yes outside 0 to 0x10FFFF, so a lookup never fails;
    - `static int Decompose(int codePoint, Span<int> destination)`: writes the full canonical decomposition, the Hangul one, or the code point itself, and returns its length, 1 to 4. `destination` holds at least `MaxDecompositionLength`;
    - `static int Compose(int first, int second)`: the primary composite, Hangul included, or −1.

- [ ] **Step 1: Failing tests** (`HangulTests`, from the Unicode Standard 3.12): U+D4DB decomposes to U+1111 U+1171 U+11B6, U+AC00 to U+1100 U+1161, and U+D7A3 to U+1112 U+1175 U+11C2. (U+1100, U+1161) → U+AC00; (U+AC00, U+11A8) → U+AC01; (U+1112, U+1175) → U+D788; (U+D788, U+11C2) → U+D7A3. −1 for (U+AC00, U+11A7), because T starts at U+11A8; (U+AC01, U+11A8), because U+AC01 already has a T; (U+1113, U+1161) and (U+1100, U+1176), which lie outside the ranges; and (U+D7A4, U+11A8), past the last syllable. `IsSyllable` is true for U+AC00 and U+D7A3 and false for U+ABFF and U+D7A4.
- [ ] **Step 2:** Verify they fail, then implement `Hangul`, and verify they pass.
- [ ] **Step 3: Failing tests** (`NormalizationLookupTests`):
  - `LookupsNeverFail`: −1, 0x110000, `int.MaxValue` and unassigned U+0378 and U+10FFFF give class 0, Yes, a decomposition of themselves (length 1), and no composite.
  - `LookupsAgreeWithTheModelForEveryCodePoint` (labelled as the encoding check): for every code point from 0 to 0x10FFFF, `CombiningClass`, `QuickCheck` and `Decompose` (non-Hangul) agree with `NormalizationModel`, and `Compose` gives exactly the model's primary composites for every pair the model lists, and −1 for (U+0071, U+0308).
  - `HangulGoesThroughTheLookups`: `Decompose(0xD4DB)` gives U+1111 U+1171 U+11B6 and `Compose(0x1100, 0x1161)` gives U+AC00.
- [ ] **Step 4:** Verify they fail, implement `NormalizationLookup` (binary search over `CodePoints` and `Pairs`), and verify they pass.

### Task 4: The normalizer

**Files:**
- Create: `src/Maviray.Pdf/Text/TextNormalizer.cs`
- Test: `tests/Maviray.Pdf.Tests/Text/TextNormalizerTests.cs`, `tests/Maviray.Pdf.Tests/Text/Unicode/NormalizationConformanceTests.cs`

**Interfaces:**
- Consumes `NormalizationLookup` and `Hangul` (Task 3).
- Produces `internal static class TextNormalizer`, `static string ToNfc(string text)`: the NFC form of `text`, each unpaired surrogate replaced by U+FFFD; `text` itself when that changes nothing. `ArgumentNullException` for `null`.

**Algorithm.** The Unicode Standard 3.11, behind UAX #15's quick check:
1. Scan the code points, remembering the index of the last code point with class 0. Stop at the first lone surrogate, Maybe or No, or class that is lower than the one before it. If there is none, return `text`.
2. From the remembered starter (or index 0) to the end: decompose each code point into a pooled `int` buffer, a lone surrogate as U+FFFD; sort each run of non-starters by class, stably and in better than quadratic time (a counting sort by class suits); compose each code point with the last starter when it isn't blocked and `Compose` gives a composite.
3. If the result equals the original code points from that index, return `text`, which allocates nothing. Otherwise return the unchanged prefix followed by the result.

- [ ] **Step 1: Failing tests** (`TextNormalizerTests`), every expectation from **Data** above or the Unicode Standard:
  - `NfcTextIsReturnedAsTheSameInstance`, a theory: `""`, `"Hello"`, `"Äpfel über Straße"`, `"q̈"`, `"क़"`, `"কা"`, `"́a"`, `"각"`, `"\U0001F600"`. Each result is the same instance (`Assert.Same`).
  - `NfcTextAllocatesNothing`: after one warm-up call, `GC.GetAllocatedBytesForCurrentThread()` doesn't change across `ToNfc("Äpfel über Straße")` and `ToNfc("q̈")`.
  - `ComposesAndReorders`, a theory of input and expected: `"Å"` → `"Å"`; `"Ḉ"` → `"Ḉ"`; `"ậ"` and `"ậ"` → `"ậ"`; `"Köln"` → `"Köln"`; `"Å"` → `"Å"`; `"Ω"` → `"Ω"`; `"क़"` → `"क़"`; `"⫝̸"` → `"⫝̸"`; `"̈́"` → `"̈́"`; `"ཱི"` → `"ཱི"`; `"\U00011099\U000110BA"` → `"\U0001109A"`; `"\U0001D15E"` → `"\U0001D157\U0001D165"`; `"각"` → `"각"`; `"각"` → `"각"`.
  - `ReplacesUnpairedSurrogates`, a theory: `"a\uD800b"` → `"a�b"`; `"\uDC00"` → `"�"`; `"x\uD83D"` → `"x�"`; `"\uDE00\uD83D"` → `"��"`; `"e\uD800́"` → `"e�́"`; `"😀\uDC00"` → `"\U0001F600�"`.
  - `KeepsTheTextBeforeTheFirstChange`: 10,000 repeats of `"ab "` then `"é"` gives the same repeats then `"é"`.
  - `LongRunsOfMarksStayFast` (async, `Timeout = 10000`, the work in `Task.Run`): `"a"` followed by 100,000 pairs of U+0301 U+0323 gives U+1EA1, then 99,999 × U+0323, then 100,000 × U+0301.
  - `RefusesNull`.
- [ ] **Step 2:** Verify they fail: `TextNormalizer` doesn't exist.
- [ ] **Step 3:** Implement `TextNormalizer.ToNfc` as **Algorithm** says. It doesn't recurse.
- [ ] **Step 4:** Verify they pass.
- [ ] **Step 5: Failing tests** (`NormalizationConformanceTests`):
  - `EveryNormalizationTestLinePasses`: for each of the 19,965 lines of `NormalizationTest.txt` (asserted), c2 = `ToNfc`(c1) = `ToNfc`(c2) = `ToNfc`(c3) and c4 = `ToNfc`(c4) = `ToNfc`(c5). The test gathers every failure and reports the first 20, each with its line number, field, expected and actual code points.
  - `EveryOtherCodePointIsUnchanged`: every code point from 0 to 0x10FFFF that isn't a surrogate and isn't one of the 17,085 code points of `@Part1` comes back unchanged.
- [ ] **Step 6:** Run them. If a line fails, the cause is found with superpowers:systematic-debugging before any change. Expected after any fix: both pass.

### Task 5: The ICU ban, both globalization modes, and CI

**Files:**
- Create: `tests/Maviray.Pdf.Tests/Text/SourceRulesTests.cs`, `tests/Maviray.Pdf.Tests/Text/GlobalizationModeTests.cs`
- Modify: `.github/workflows/build.yml`, `CONTRIBUTING.md`

- [ ] **Step 1: Tests** (`SourceRulesTests`), over the files found from `src/Maviray.Pdf/Maviray.Pdf.csproj` upward:
  - `NothingInSrcCallsDotNetNormalization`: no `.cs` file in `src/` matches `\b(Normalize|IsNormalized)\s*\(`. A failure names each file and line.
  - `TextEngineUsesNoCulture`: no `.cs` file in `src/Maviray.Pdf/Text/` matches `\b(CultureInfo|CompareInfo|TextInfo)\b`.
- [ ] **Step 2:** Watch each fail on a temporary mutant: add `_ = "a".Normalize();` to `TextNormalizer.cs`, then a comment naming `CultureInfo`. Expected: each test fails naming the file and line. Remove both mutants, and verify the tests pass.
- [ ] **Step 3: Test** (`GlobalizationModeTests.RunsInTheModeTheEnvironmentAsks`): when `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT` is `true` or `1`, `"é".Normalize()` returns `"é"`, so .NET's normalization is off; otherwise it returns `"é"`. This proves that the second run really runs without ICU. The file sits in `tests/`, outside the ban.
- [ ] **Step 4:** Run the text tests in both modes locally: `dotnet test --project tests/Maviray.Pdf.Tests --filter-namespace "Maviray.Pdf.Tests.Text" --filter-namespace "Maviray.Pdf.Tests.Text.*"`, once as it is and once with `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true`. Expected: both pass, with the same count, and the count equals all the tests in the two namespaces.
- [ ] **Step 5:** Add a step to `build.yml` after "Test": "Test the text engine without ICU", with `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT: true` and `MAVIRAY_PDF_REQUIRE_VALIDATORS: true`, running `dotnet test --no-build -c "$CONFIGURATION" --filter-namespace "Maviray.Pdf.Tests.Text" --filter-namespace "Maviray.Pdf.Tests.Text.*"`, with a comment citing T-01.
- [ ] **Step 6:** Add a "Unicode data" section to `CONTRIBUTING.md`: where the files are and why they are compressed; that the tables in `src/Maviray.Pdf/Text/Unicode/` are generated and never edited by hand; how to regenerate them (`MAVIRAY_PDF_WRITE_UNICODE_TABLES=true` with the Step 8 command of Task 2); and how to run the text tests without ICU (the Step 4 command).
- [ ] **Step 7:** Run the full suite. Expected: all pass, no build warnings.
- [ ] **Step 8: Commit** `src/Maviray.Pdf/Text/`, the new test files, `.gitattributes`, `build.yml` and `CONTRIBUTING.md`: `#30 add NFC normalization, generated from the Unicode data and checked by NormalizationTest`.

### Task 6: Records

**Files:**
- Create: `docs/releases/0.1.0/execution-logs/2026-11-23-issue-30-unicode-data-nfc.md`
- Modify: `docs/releases/0.1.0/README.md`, `docs/releases/0.1.0/specs/2026-11-23-p1-text-design.md`

- [ ] **Step 1:** In T-04 of the spec, "in all four parts" becomes "in every part". It is an amendment while the work is open, with no decision changed.
- [ ] **Step 2:** Add the row for #30 to the release index ("In review, 2026-11-23", with links to this plan and its log). Write the execution log.
- [ ] **Step 3: Commit** the plan, the log, the index and the spec: `#30 add the P1.5 plan and execution log`. Push, and open the pull request into `develop`.
