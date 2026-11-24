# P1.6 Line breaking: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `LineBreaker.FindBreaks`, UAX #14's default algorithm for Unicode 16.0, which passes every test of `LineBreakTest.txt`; grapheme clusters from `StringInfo`; and `BreakOpportunities.Find`, which drops a break that falls inside a cluster.

**Architecture:** The P1.5 generator gains a line-break model, read from `LineBreak.txt`, `EastAsianWidth.txt`, `emoji-data.txt` and `UnicodeData.txt`, and writes a fourth table: each code point's class after LB1, with the flags the rules test. The breaker groups the code points into LB9 items and decides each boundary between items by running the rules in order, the first that matches deciding.

**Tech Stack:** .NET 10, C#, xUnit v3 on MTP, `System.Globalization.StringInfo`.

**Spec:** [P1 Text](../specs/2026-11-23-p1-text-design.md), decisions T-05 and T-06. Work item [#31](https://github.com/Maviray/pdf/issues/31). It builds on P1.5 ([#30](https://github.com/Maviray/pdf/issues/30), PR #40): the data files, `UcdFiles`, `RepositoryFiles` and the generator.

## Global Constraints

- Every P1.5 constraint holds: `internal` types, handwritten code in `src/Maviray.Pdf/Text/` (namespace `Maviray.Pdf.Text`), generated tables only in `src/Maviray.Pdf/Text/Unicode/`, one type per file, no partial classes, no ICU and no culture, `static ReadOnlySpan<T>` tables, warnings as errors, trimming- and AOT-safe.
- Test sources spell every character beyond ASCII as an escape (P1.5's `TextTestsSpellNonAsciiAsEscapes`). Tool input turns typed `\u` escapes into the characters themselves, so tests build such strings from code points (`char.ConvertFromUtf32`) or write them with the file tools and check them with that test.
- Expected values come from the UCD files, `LineBreakTest.txt` or UAX #14 for 16.0 (revision 53). UAX #14 is transcribed below, under **Rules**, from the standard as the implementer knows it, because reading it online wasn't permitted here. `LineBreakTest.txt` is the judge: a rule that disagrees with it is corrected, and the correction is a ruling.
- Breaking runs in linear time in the length of the text, whatever the input.
- The work is committed as two commits, the code and the records.

## Decisions this plan makes

- **Positions.** `FindBreaks` returns one `LineBreakKind` per UTF-16 boundary, `text.Length + 1` entries. Entry *i* is the kind of the boundary before `text[i]`. Entry 0 is `None` (LB2). The last entry is `Required` (LB3), except for empty text, whose only entry is `None`. A boundary inside a surrogate pair is `None`.
- **Kinds.** `Required` comes only from LB4 and LB5, the break characters of T-06. Every other `÷` is `Allowed`.
- **Items.** Rules LB11 to LB31 see items: a code point with every CM and ZWJ that LB9 attaches to it. An item has the class of its base and the base's flags. LB8a looks at the last code point of the item before the boundary, because a ZWJ there may have been attached.
- **Clusters.** `GraphemeClusters.FindStarts` marks where each cluster starts, from `StringInfo.GetNextTextElementLength`. `BreakOpportunities.Find` is `FindBreaks` with every `Allowed` inside a cluster made `None`. A `Required` never falls inside a cluster, because a break character is a control in UAX #29, and a test asserts it.

## Data

Values read from the files with a throwaway script, independent of the code below:
- `LineBreak.txt` 16.0.0 uses 48 classes: AI AK AL AP AS B2 BA BB BK CB CJ CL CM CP CR EB EM EX GL H2 H3 HL HY ID IN IS JL JT JV LF NL NS NU OP PO PR QU RI SA SG SP SY VF VI WJ XX ZW ZWJ. Its only `@missing` line is `0000..10FFFF; XX`. `EastAsianWidth.txt`'s only one is `0000..10FFFF; N`.
- Raw class, general category and East Asian width: U+00A7 AI, Po, A; U+D800 SG, Cs; U+0378 XX, Cn; U+0E01 SA, Lo; U+0E31 SA, Mn; U+102B SA, Mc; U+3041 CJ, Lo, W; U+000B, U+000C, U+2028 and U+2029 BK; U+0085 NL; U+00AB QU, Pi, N; U+00BB QU, Pf, N; U+201C QU, Pi, A; U+201D QU, Pf, A; U+0022 QU, Po; U+2010 BA; U+25CC AL; U+FFFD AI; U+3400 ID, W; U+1F466 EB, W, Extended_Pictographic; U+1F3FB EM; U+1FFFD ID, Cn, Extended_Pictographic.
- 1,477 code points are both `Extended_Pictographic` and unassigned.
- With 128 code points per block, the line-break table has 210 distinct blocks, so its index fits in bytes: about 62 KB of data.
- `LineBreakTest.txt` holds 16,672 tests.

## Rules

UAX #14 for 16.0, as `FindBreaks` applies them. `×` is no break, `÷` a break, `!` a required break. `$EA` is East Asian width F, W or H. Each boundary takes the first rule that matches.

- LB1: AI, SG and XX resolve to AL; SA to CM for Mn and Mc, otherwise AL; CJ to NS.
- LB2 `sot ×`; LB3 `! eot`.
- LB4 `BK !`. LB5 `CR × LF`, `CR !`, `LF !`, `NL !`. LB6 `× (BK | CR | LF | NL)`. LB7 `× SP`, `× ZW`. LB8 `ZW SP* ÷`. LB8a `ZWJ ×`.
- LB9: a CM or ZWJ after any class but BK, CR, LF, NL, SP and ZW joins that item, with `×` before it. LB10: any other CM or ZWJ is AL.
- LB11 `× WJ`, `WJ ×`. LB12 `GL ×`. LB12a `[^SP BA HY] × GL`. LB13 `× CL`, `× CP`, `× EX`, `× SY`. LB14 `OP SP* ×`.
- LB15a `(sot | BK | CR | LF | NL | OP | QU | GL | SP | ZW) [QU & Pi] SP* ×`. LB15b `× [QU & Pf] (SP | GL | WJ | CL | QU | CP | EX | IS | SY | BK | CR | LF | NL | ZW | eot)`. LB15c `SP ÷ IS NU`. LB15d `× IS`.
- LB16 `(CL | CP) SP* × NS`. LB17 `B2 SP* × B2`. LB18 `SP ÷`.
- LB19 `× [QU - Pi]`, `[QU - Pf] ×`. LB19a `[^$EA] × QU`, `× QU ([^$EA] | eot)`, `QU × [^$EA]`, `(sot | [^$EA]) QU ×`.
- LB20 `÷ CB`, `CB ÷`. LB20a `(sot | BK | CR | LF | NL | SP | ZW | CB | GL) (HY | U+2010) × AL`. *Corrected during Task 3:* this plan first had `× (AL | HL)`, and four lines of `LineBreakTest.txt` expect a break between a word-initial hyphen and a Hebrew letter.
- LB21 `× BA`, `× HY`, `× NS`, `BB ×`. LB21a `HL (HY | [BA - $EA]) × [^HL]`. LB21b `SY × HL`.
- LB22 `× IN`. LB23 `(AL | HL) × NU`, `NU × (AL | HL)`. LB23a `PR × (ID | EB | EM)`, `(ID | EB | EM) × PO`. LB24 `(PR | PO) × (AL | HL)`, `(AL | HL) × (PR | PO)`.
- LB25: `NU (SY | IS)* (CL | CP)? × (PO | PR)`; `(PO | PR) × OP NU`; `(PO | PR) × OP IS NU`; `(PO | PR) × NU`; `HY × NU`; `IS × NU`; `NU (SY | IS)* × NU`.
- LB26 `JL × (JL | JV | H2 | H3)`, `(JV | H2) × (JV | JT)`, `(JT | H3) × JT`. LB27 `(JL | JV | JT | H2 | H3) × PO`, `PR × (JL | JV | JT | H2 | H3)`.
- LB28 `(AL | HL) × (AL | HL)`. LB28a, with ◌ for U+25CC: `AP × (AK | ◌ | AS)`, `(AK | ◌ | AS) × (VF | VI)`, `(AK | ◌ | AS) VI × (AK | ◌)`, `(AK | ◌ | AS) × (AK | ◌ | AS) VF`.
- LB29 `IS × (AL | HL)`. LB30 `(AL | HL | NU) × [OP - $EA]`, `[CP - $EA] × (AL | HL | NU)`. LB30a: between two RI, `×` when an odd number of RI precede the boundary in an unbroken run. LB30b `EB × EM`, `[Extended_Pictographic & Cn] × EM`.
- LB31 `÷`.

## Review Focus

1. **Rules that look back over a run** (LB8, LB14, LB15a, LB16, LB17 over spaces; LB25 over SY and IS; LB30a over RI): a run of 100,000 stays linear. Pinned in Task 3, with a time limit.
2. **CM and ZWJ at the edges**: first in the text, after a space, after a break character, and a ZWJ attached to a base before an emoji. Pinned in Task 3, and by the conformance file.
3. **Supplementary code points**: a boundary inside a surrogate pair is never a break, and an unpaired surrogate (which P1.5 replaces before this runs) still gets a class. Pinned in Task 3.
4. **Breaks that UAX #14 allows inside a cluster**: a space followed by a combining mark. `BreakOpportunities` drops them. Pinned in Task 4.
5. **Required breaks**: each break character, CR LF as one break, and text ending in a break character. Pinned in Task 3.

---

### Task 1: The line-break model and its table

**Files:**
- Modify: `tests/Maviray.Pdf.Tests/Text/Unicode/UcdFiles.cs` (adds `ReadMissing`), `UnicodeTableGenerator.cs` (adds the fourth table)
- Create (tests): `tests/Maviray.Pdf.Tests/Text/Unicode/LineBreakModel.cs`
- Create (src): `src/Maviray.Pdf/Text/LineBreakClass.cs`; generated `src/Maviray.Pdf/Text/Unicode/LineBreakData.cs`
- Test: `tests/Maviray.Pdf.Tests/Text/Unicode/LineBreakModelTests.cs`

**Interfaces:**
- Produces `internal enum LineBreakClass : byte` (src), the 48 classes above in that alphabetical order, `AI = 0` to `ZWJ = 47`.
- Produces `UcdFiles.ReadMissing(string fileName)`: the `(First, Last, Value)` of each `# @missing:` line.
- Produces `internal sealed class LineBreakModel` (tests), `static LineBreakModel Load()`, shared:
  - `LineBreakClass RawClass(int codePoint)`, `string GeneralCategory(int codePoint)` (`Cn` when unlisted), `bool IsEastAsian(int codePoint)` (F, W or H), `bool IsExtendedPictographic(int codePoint)`;
  - `LineBreakClass ResolvedClass(int codePoint)`, after LB1;
  - `bool IsInitialQuote(int codePoint)` and `bool IsFinalQuote(int codePoint)`: a QU with general category Pi, or Pf;
  - `bool IsUnassignedPictographic(int codePoint)`: `Extended_Pictographic` and `Cn`;
  - `static IReadOnlyList<string> SourceFiles`: `LineBreak.txt`, `EastAsianWidth.txt`, `emoji-data.txt`, `UnicodeData.txt`.
- Produces the generated `LineBreakData` (`Maviray.Pdf.Text.Unicode`): `const int BlockShift = 7`, `ReadOnlySpan<byte> BlockIndex`, `ReadOnlySpan<ushort> Blocks`. An entry holds the resolved class in bits 0 to 5, East Asian in bit 6, initial quote in bit 7, final quote in bit 8 and unassigned pictographic in bit 9. The generator refuses more than 256 blocks, as for `NfcPropertyData`.

- [ ] **Step 1: Failing tests** (`LineBreakModelTests`), with the values from **Data**:
  - `RawClassesAndProperties`: a theory over the code points listed there, each with its raw class, general category and East Asian width.
  - `Lb1Resolutions`: U+00A7, U+D800, U+0378, U+0E01, U+FFFD → AL; U+0E31 and U+102B → CM; U+3041 → NS; U+0041 → AL.
  - `QuotesAndPictographs`: U+00AB and U+201C initial, U+00BB and U+201D final, U+0022 neither; 1,477 unassigned pictographic code points, U+1FFFD among them and U+1F466 not.
  - `UsesTheFortyEightClasses`: every class of `LineBreakClass` occurs in `LineBreak.txt`, and no other.
- [ ] **Step 2:** Verify they fail to compile, then implement `LineBreakClass`, `UcdFiles.ReadMissing` and `LineBreakModel`. A file's `@missing` values apply first and its data lines override them. `UnicodeData.txt`'s `First>`/`Last>` ranges count as assigned. Verify they pass.
- [ ] **Step 3:** Add `LineBreakData` to `UnicodeTableGenerator.Generate`. Run the P1.5 staleness test: expected to fail, naming the missing `LineBreakData.cs`. Write the tables with `MAVIRAY_PDF_WRITE_UNICODE_TABLES=true`, build, and verify the staleness, folder and determinism tests pass. Record the table's size in the log.

### Task 2: Line-break properties

**Files:**
- Create: `src/Maviray.Pdf/Text/LineBreakProperties.cs`
- Test: `tests/Maviray.Pdf.Tests/Text/LineBreakPropertiesTests.cs`

**Interfaces:**
- Produces `internal static class LineBreakProperties`: `LineBreakClass Class(int codePoint)`, `bool IsEastAsian(int codePoint)`, `bool IsInitialQuote(int codePoint)`, `bool IsFinalQuote(int codePoint)`, `bool IsUnassignedPictographic(int codePoint)`. A value outside 0 to 0x10FFFF gets AL and no flags, LB1's resolution of XX.

- [ ] **Step 1: Failing tests:** `LookupsAgreeWithTheModelForEveryCodePoint`, labelled as the encoding check, as in P1.5; `LookupsNeverFail` for −1, 0x110000 and `int.MaxValue`.
- [ ] **Step 2:** Verify they fail, implement, and verify they pass.

### Task 3: The line breaker

**Files:**
- Create: `src/Maviray.Pdf/Text/LineBreakKind.cs`, `src/Maviray.Pdf/Text/LineBreaker.cs`
- Test: `tests/Maviray.Pdf.Tests/Text/LineBreakerTests.cs`, `tests/Maviray.Pdf.Tests/Text/Unicode/LineBreakConformanceTests.cs`

**Interfaces:**
- Consumes `LineBreakProperties` (Task 2).
- Produces `internal enum LineBreakKind : byte { None = 0, Allowed = 1, Required = 2 }`.
- Produces `internal static class LineBreaker`, `static LineBreakKind[] FindBreaks(string text)`, as **Decisions** says. `ArgumentNullException` for `null`. A lone surrogate is read as its own code unit, which has class SG and resolves to AL.

- [ ] **Step 1: Failing tests** (`LineBreakerTests`), each written as the text and the kinds at its boundaries:
  - `BreakCharactersRequireABreak`: after each of U+000B, U+000C, U+2028, U+2029, U+000D, U+000A and U+0085, followed by `a`, the boundary is `Required`.
  - `CrLfIsOneBreak`: `"a\r\nb"`: `None` between CR and LF, `Required` after LF.
  - `EndsWithARequiredBreak`: the last entry of `"a"` is `Required`, and of `"a\n"` too; `""` gives a single `None`.
  - `SpacesHangBeforeTheBreak`: `"ab cd"` allows a break before `c` only, and `"ab  cd"` likewise after both spaces.
  - `NoBreakInsideASurrogatePair`: U+1F600 U+1F600 gives `None` inside each pair.
  - `MarksAtTheEdges`: a leading U+0301, U+0301 after a space, and U+0301 after U+000A, each as the conformance file expects, by its own lines (LB10, LB18, LB4).
  - `LongRunsStayLinear` (async, `Timeout = 10000`): `(` with 100,000 spaces then `a`; 100,000 regional indicators; `1` with 100,000 × `.` then `1`. Each finishes, and spot boundaries are right: no break before the `a` (LB14), a break between every second pair of RI only, and no break before the last `1` (LB25).
- [ ] **Step 2:** Verify they fail to compile.
- [ ] **Step 3:** Implement `LineBreaker.FindBreaks` from **Rules**. Read the code points and their classes; group them into items (LB9, LB10); decide LB4 to LB8a at each boundary; then decide each boundary between items with LB11 to LB31, in order. A run before the boundary is never scanned twice: keep, for each item, where its run of spaces, of SY and IS, and of RI began.
- [ ] **Step 4:** Verify `LineBreakerTests` pass.
- [ ] **Step 5: Failing test** (`LineBreakConformanceTests.EveryLineBreakTestPasses`): each of the 16,672 lines (asserted) is parsed into its text and the `×` or `÷` at each code point boundary. `FindBreaks` gives `None` where the line has `×`, and `Allowed` or `Required` where it has `÷`, at every boundary after the first. The test gathers every failure and reports the first 20, each with its line number, boundary, expected and actual, and the rule the line's comment names.
- [ ] **Step 6:** Run it. A failing line is diagnosed with superpowers:systematic-debugging against its comment's rule number before any change. A rule of **Rules** that the file contradicts is corrected as a ruling. Expected after any fix: every line passes.

### Task 4: Grapheme clusters and break opportunities

**Files:**
- Create: `src/Maviray.Pdf/Text/GraphemeClusters.cs`, `src/Maviray.Pdf/Text/BreakOpportunities.cs`
- Test: `tests/Maviray.Pdf.Tests/Text/GraphemeClustersTests.cs`, `tests/Maviray.Pdf.Tests/Text/BreakOpportunitiesTests.cs`

**Interfaces:**
- Produces `internal static class GraphemeClusters`, `static bool[] FindStarts(string text)`: `text.Length + 1` entries, true where a cluster starts and at `text.Length`.
- Produces `internal static class BreakOpportunities`, `static LineBreakKind[] Find(string text)`: `FindBreaks` with each `Allowed` inside a cluster made `None`.

- [ ] **Step 1: Failing tests** (`GraphemeClustersTests`, T-05's cases), each built from code points: `e` U+0301, and Cyrillic U+043E U+0301 and U+0430 U+0301 U+0308, are one cluster each; CR LF is one cluster; U+1F1E9 U+1F1EA is one cluster, and three regional indicators are two; U+1F468 U+200D U+1F469 U+200D U+1F467 is one cluster; `ab` is two.
- [ ] **Step 2:** Verify they fail, implement, and verify they pass.
- [ ] **Step 3: Failing tests** (`BreakOpportunitiesTests`):
  - `DropsBreaksInsideClusters`: in `a`, space, U+0301, `b`, `FindBreaks` allows a break between the space and U+0301 (LB18), and `Find` doesn't.
  - `KeepsBreaksBetweenClusters`: `"ab cd"` gives the same kinds as `FindBreaks`.
  - `RequiredBreaksAreClusterStarts`: for every line of `LineBreakTest.txt`, each `Required` boundary of `FindBreaks` is a cluster start.
- [ ] **Step 4:** Verify they fail, implement, verify they pass, and run the full suite in Debug and Release and the text tests without ICU. Expected: all pass, no warnings.
- [ ] **Step 5: Commit** the code and tests: `#31 add UAX #14 line breaking, checked by LineBreakTest`.

### Task 5: Records

- [ ] **Step 1:** Add the row for #31 to the release index, and write the execution log.
- [ ] **Step 2: Commit** the plan, the log and the index: `#31 add the P1.6 plan and execution log`. Push, and open the pull request into `develop`, stacked on #40.
